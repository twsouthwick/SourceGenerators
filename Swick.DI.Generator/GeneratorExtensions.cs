// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

internal static class GeneratorExtensions
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

                   var result = new ContainerRegistration();
                   var builder = ImmutableArray.CreateBuilder<Item>();

                   foreach (var attribute in method.GetAttributes())
                   {
                       if (SymbolEqualityComparer.Default.Equals(optionsAttribute, attribute.AttributeClass))
                       {
                           result = result with { Options = BuildOptions(result.Options, attribute) };
                       }
                       else if (SymbolEqualityComparer.Default.Equals(registerAttribute, attribute.AttributeClass))
                       {
                           builder.Add(AddRegistration(attribute));
                       }
                       else if (SymbolEqualityComparer.Default.Equals(registerFactoryAttribute, attribute.AttributeClass))
                       {
                           builder.Add(AddFactoryRegistration(attribute));
                       }
                   }

                   return result with { Items = builder.ToImmutable() };
               })
           .Where(t => t is { Items: { } items } && !items.IsDefaultOrEmpty)!;

        //var types = context.CompilationProvider
        //    .Select((compilation, token) => new KnownTypes
        //    {
        //        KnownFeature = compilation.GetTypeByMetadataName("Swick.DependencyInjection.RegisterAttribute"),
        //        ThreadSafe = compilation.GetTypeByMetadataName("Swick.DependencyInjection.ContainerOptionsAttribute"),
        //        Delegated = compilation.GetTypeByMetadataName("Swick.DependencyInjection.RegisterFactoryAttribute"),
        //    });
    }

    private static Item AddRegistration(AttributeData data)
    {
        return new Item { };
    }

    private static Item AddFactoryRegistration(AttributeData data)
    {
        return new Item { };
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
}