﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Swick.DependencyInjection.Generator.Models;
using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

internal static class ContainerRegistrationExtensions
{
    public static IncrementalValuesProvider<ContainerRegistration> GetContainerRegistrations(this IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
           .CreateSyntaxProvider(
               (node, token) =>
               {
                   if (node.IsKind(SyntaxKind.MethodDeclaration) && node is MethodDeclarationSyntax method)
                   {
                       foreach (var list in method.AttributeLists)
                       {
                           foreach (var attribute in list.Attributes)
                           {
                               if (attribute.Name.ToString().Contains("Register"))
                               {
                                   return true;
                               }
                           }
                       }
                   }

                   return false;
               },
               (t, token) =>
               {
                   var registerAttribute = t.SemanticModel.Compilation.GetTypeByMetadataName("Swick.DependencyInjection.RegisterAttribute");
                   var registerFactoryAttribute = t.SemanticModel.Compilation.GetTypeByMetadataName("Swick.DependencyInjection.RegisterFactoryAttribute");
                   var optionsAttribute = t.SemanticModel.Compilation.GetTypeByMetadataName("Swick.DependencyInjection.ContainerOptionsAttribute");

                   if (registerAttribute is null || registerFactoryAttribute is null || optionsAttribute is null)
                   {
                       return null;
                   }

                   if (t.SemanticModel.GetDeclaredSymbol(t.Node, token) is not IMethodSymbol method)
                   {
                       return null;
                   }

                   var details = BuildDetails(method);
                   var result = new ContainerRegistration(details);
                   var builder = ImmutableArray.CreateBuilder<Registration>();
                   var errors = ImmutableArray.CreateBuilder<Error>();
                   var registeredService = new HashSet<string>();

                   foreach (var attribute in method.GetAttributes())
                   {
                       if (SymbolEqualityComparer.Default.Equals(optionsAttribute, attribute.AttributeClass))
                       {
                           result = result with { Options = BuildOptions(result.Options, attribute) };
                       }
                       else if (SymbolEqualityComparer.Default.Equals(registerAttribute, attribute.AttributeClass))
                       {
                           if (AddRegistration(attribute) is { } registration)
                           {
                               if (registeredService.Add(registration.ServiceType.FullName))
                               {
                                   builder.Add(registration);
                               }
                               else
                               {
                                   var location = attribute.ApplicationSyntaxReference?.GetSyntax(token)?.GetLocation();
                                   errors.Add(new Error(KnownErrors.DuplicateService, location, registration.ServiceType.FullName, registration.ImplementationType.FullName));
                               }
                           }
                       }
                       else if (SymbolEqualityComparer.Default.Equals(registerFactoryAttribute, attribute.AttributeClass))
                       {
                           if (AddFactoryRegistration(method.ContainingType, attribute) is { } registration)
                           {
                               builder.Add(registration);
                           }
                       }
                   }

                   return result with
                   {
                       Registrations = builder.ToImmutable(),
                       Errors = errors.ToImmutable(),
                   };
               })
           .Where(t => t is { Registrations: { } items } && !items.IsDefaultOrEmpty)!;
    }

    private static TypeRegistration? AddRegistration(AttributeData data)
    {
        if (data.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol service }, { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol impl }])
        {
            return new TypeRegistration(CreateTypeReference(service))
            {
                ImplementationType = CreateTypeReference(impl),
            };
        }
        else if (data.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol contract }, { Kind: TypedConstantKind.Type, Value: null }])
        {
            var reference = CreateTypeReference(contract);
            return new TypeRegistration(reference)
            {
                ImplementationType = reference,
            };
        }
        else if (data.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol type }])
        {
            var reference = CreateTypeReference(type);
            return new TypeRegistration(reference)
            {
                ImplementationType = reference,
            };
        }

        return null;
    }

    private static TypeReference CreateTypeReference(INamedTypeSymbol type)
    {
        var constructor = type.Constructors.OrderBy(c => c.Parameters.Length).FirstOrDefault();
        var parameters = constructor is null
            ? ImmutableArray<TypeName>.Empty
            : constructor.Parameters
                         .Select(t => new TypeName(t.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                         .ToImmutableArray();

        return new TypeReference(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), parameters, type.TypeKind);
    }

    private static Registration? AddFactoryRegistration(INamedTypeSymbol containingType, AttributeData data)
    {
        if (data.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol service }, { Value: string name }] &&
            containingType.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(f => f.Parameters.Length == 0) is { } method)
        {
            var reference = CreateTypeReference(service);
            return new FactoryRegistration(reference, MethodReference.Create(method));
        }

        return null;
    }

    private static ContainerOptions BuildOptions(ContainerOptions options, AttributeData data)
    {
        foreach (var arg in data.NamedArguments)
        {
            if (string.Equals("IsThreadSafe", arg.Key, StringComparison.Ordinal) && arg.Value.Value is bool isThreadSafe)
            {
                options = options with { IsThreadSafe = isThreadSafe };
            }
        }

        return options;
    }

    private static ContainerDetails BuildDetails(IMethodSymbol method)
    {
        var stack = ImmutableStack.Create<Visible<TypeReference>>();
        var type = method.ContainingType;
        var ns = type.ContainingNamespace.ToString();

        while (type is not null)
        {
            stack = stack.Push(new(CreateTypeReference(type), type.DeclaredAccessibility));
            type = type.ContainingType;
        }

        return new ContainerDetails(ns, stack, new(new(method.Name), method.DeclaredAccessibility));
    }
}