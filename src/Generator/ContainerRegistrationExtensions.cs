// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Swick.Features.Generator.Models;
using System.Collections.Immutable;

namespace Swick.Features.Generator;

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
                   var registerAttribute = t.SemanticModel.Compilation.GetTypeByMetadataName("Swick.Features.RegisterAttribute");
                   var registerFactoryAttribute = t.SemanticModel.Compilation.GetTypeByMetadataName("Swick.Features.RegisterFactoryAttribute");
                   var optionsAttribute = t.SemanticModel.Compilation.GetTypeByMetadataName("Swick.Features.ContainerOptionsAttribute");

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
                   var errors = result.Errors.ToBuilder();
                   var registeredService = new HashSet<string>();

                   foreach (var attribute in method.GetAttributes())
                   {
                       if (SymbolEqualityComparer.Default.Equals(optionsAttribute, attribute.AttributeClass))
                       {
                           result = result with { Options = BuildOptions(result.Options, attribute, errors, method) };
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
                               if (registeredService.Add(registration.ServiceType.FullName))
                               {
                                   builder.Add(registration);
                               }
                           }
                           else
                           {
                               var location = attribute.ApplicationSyntaxReference?.GetSyntax(token)?.GetLocation();
                               errors.Add(new Error(KnownErrors.InvalidFactory, location));
                           }
                       }
                   }

                   return result with
                   {
                       Registrations = builder.ToImmutable(),
                       Errors = errors.ToImmutable(),
                   };
               })
           .Where(t => t is not null)!;
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

    private static FactoryRegistration? AddFactoryRegistration(INamedTypeSymbol containingType, AttributeData data)
    {
        if (data.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol service }, { Value: string name }] &&
            containingType.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(f => f.Parameters.Length == 0) is { } method &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, service))
        {
            var reference = CreateTypeReference(service);
            return new FactoryRegistration(reference, MethodReference.Create(method));
        }

        return null;
    }

    private static ContainerOptions BuildOptions(ContainerOptions options, AttributeData data, ImmutableArray<Error>.Builder errors, IMethodSymbol method)
    {
        foreach (var arg in data.NamedArguments)
        {
            if (string.Equals("IsThreadSafe", arg.Key, StringComparison.Ordinal) && arg.Value.Value is bool isThreadSafe)
            {
                options = options with { IsThreadSafe = isThreadSafe };
            }
            else if (string.Equals("SetMethod", arg.Key, StringComparison.Ordinal) && arg.Value.Value is string setMethod)
            {
                if (method.ContainingType.GetMembers(setMethod) is [IMethodSymbol setMethodSymbol] &&
                    IsReturnable(setMethodSymbol, out var isReturnable) &&
                    setMethodSymbol.IsGenericMethod &&
                    setMethodSymbol.TypeArguments is [{ } generic] &&
                    setMethodSymbol.Parameters is [{ } p] &&
                    SymbolEqualityComparer.Default.Equals(p.Type, generic))
                {
                    options = options with { SetMethod = new(new(setMethod, isReturnable, generic.Name), setMethodSymbol.DeclaredAccessibility) };
                }
                else
                {
                    errors.Add(new(KnownErrors.InvalidSetMethod, data.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
                }
            }
        }

        return options;
    }

    private static bool IsReturnable(IMethodSymbol setMethodSymbol, out bool isReturnable)
    {
        if (setMethodSymbol.ReturnsVoid)
        {
            isReturnable = false;
            return true;
        }

        if (setMethodSymbol.ReturnType.Name == "Boolean")
        {
            isReturnable = true;
            return true;
        }

        isReturnable = true;
        return false;
    }

    private static ContainerDetails BuildDetails(IMethodSymbol method)
    {
        var stack = ImmutableStack.Create<Visible<TypeReference>>();
        var type = method.ContainingType;
        var ns = type.ContainingNamespace.ToString();
        var typeParam = method.TypeArguments.Length == 1 ? method.TypeArguments[0].Name : string.Empty;

        while (type is not null)
        {
            stack = stack.Push(new(CreateTypeReference(type), type.DeclaredAccessibility));
            type = type.ContainingType;
        }

        var errors = ImmutableArray.CreateBuilder<Error>();
        var location = method.Locations.FirstOrDefault(static m => m.IsInSource);

        if (method.Parameters.Length != 0)
        {
            errors.Add(new Error(KnownErrors.InvalidGenericGetMethod, location));
        }

        if (method.TypeArguments.Length != 1)
        {
            errors.Add(new Error(KnownErrors.InvalidGenericGetMethod, location));
        }
        else if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, method.TypeArguments[0]))
        {
            errors.Add(new Error(KnownErrors.InvalidGenericGetMethod, location));
        }

        return new ContainerDetails(ns, stack, new(new(method.Name), method.DeclaredAccessibility), typeParam)
        {
            Errors = errors.ToImmutable()
        };
    }
}