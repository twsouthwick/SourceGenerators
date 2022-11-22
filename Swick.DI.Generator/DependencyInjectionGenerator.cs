﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

[Generator]
public class DependencyInjectionGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DuplicateAttribute = new("OOX1000", "Duplicate known features", "Service {0} is already registered for {1}", "KnownFeatures", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor SingleContractForFeature = new("OOX1001", "Duplicate contracts registered", "Can only register a single contract for {0}", "KnownFeatures", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidFactoryMethod = new("OOX1002", "Invalid factory method", "Method {0} must have no parameters and return {1} type", "KnownFeatures", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidDelegatedFeatures = new("OOX1003", "Invalid delegated features", "Member {0} must have no parameters if a method and return IFeatureCollection", "KnownFeatures", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrations = context.GetContainerRegistrations();
        //var methods = context.SyntaxProvider
        //    .CreateSyntaxProvider(
        //        (node, token) =>
        //        {
        //            if (node.IsKind(SyntaxKind.MethodDeclaration) && node is MethodDeclarationSyntax method)
        //            {
        //                foreach (var list in method.AttributeLists)
        //                {
        //                    foreach (var attribute in list.Attributes)
        //                    {
        //                        if (attribute.Name.ToString().Contains("KnownFeature"))
        //                        {
        //                            return true;
        //                        }
        //                    }
        //                }
        //            }

        //            return false;
        //        },
        //        (t, token) => (t.SemanticModel.GetDeclaredSymbol(t.Node, token) as IMethodSymbol)!)
        //    .Where(t => t is not null)
        //    .Collect();

        //var types = context.CompilationProvider
        //    .Select((compilation, token) => new KnownTypes
        //    {
        //        KnownFeature = compilation.GetTypeByMetadataName("DocumentFormat.OpenXml.KnownFeatureAttribute"),
        //        ThreadSafe = compilation.GetTypeByMetadataName("DocumentFormat.OpenXml.ThreadSafeAttribute"),
        //        Delegated = compilation.GetTypeByMetadataName("DocumentFormat.OpenXml.DelegatedFeatureAttribute"),
        //        FeatureCollection = compilation.GetTypeByMetadataName("DocumentFormat.OpenXml.Features.IFeatureCollection"),
        //    });

        context.RegisterSourceOutput(registrations, static (context, registrations) =>
        {
            var typeNames = string.Join(".", registrations.Details.TypeNames.Select(t => t.Item.Name));
            var fileName = $"{registrations.Details.Namespace}.{typeNames}.{registrations.Details.Method.Item.Name}";
            var source = registrations.Build();

            context.AddSource(fileName, source);
        });

        context.RegisterPostInitializationOutput(context =>
        {
            var sb = new StringWriter();
            var indented = new IndentedTextWriter(sb);

            const string Source = """
            // <auto-generated />

            #nullable enable

            namespace Swick.DependencyInjection;

            [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
            internal sealed class RegisterAttribute : global::System.Attribute
            {
                public RegisterAttribute(global::System.Type contract, global::System.Type? service = null)
                {
                }
            }
            
            [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
            internal sealed class RegisterFactoryAttribute : global::System.Attribute
            {
                public RegisterFactoryAttribute(global::System.Type contract, global::System.Type? service = null)
                {
                }

                public bool IsExternal { get; set; }
            }
            
            [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
            internal sealed class ContainerOptionsAttribute : global::System.Attribute
            {
                public bool IsThreadSafe { get; set; }
            }

            """;

            context.AddSource("DependencyInjectionAttributes", Source);
        });
    }

    private static void Execute(SourceProductionContext context, KnownTypes knownTypes, ImmutableArray<IMethodSymbol> methods)
    {
        if (!knownTypes.IsAvailable)
        {
            return;
        }

        foreach (var method in methods)
        {
            var contracts = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var features = new HashSet<(INamedTypeSymbol Contract, ISymbol Service)>();
            var delegatedFeatures = new List<ISymbol>();
            var isThreadSafe = false;

            foreach (var attribute in method.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownTypes.ThreadSafe))
                {
                    isThreadSafe = true;
                }
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownTypes.Delegated))
                {
                    if (attribute.ConstructorArguments[0].Value is string methodName)
                    {
                        var container = attribute.ConstructorArguments[1].Value as INamedTypeSymbol ?? method.ContainingType;
                        var members = container.GetMembers(methodName);

                        if (members.Length == 1 && members[0] is ISymbol delegatedSymbol && IsValidMember(delegatedSymbol))
                        {
                            delegatedFeatures.Add(delegatedSymbol);
                        }
                        else
                        {
                            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation();
                            context.ReportDiagnostic(Diagnostic.Create(InvalidDelegatedFeatures, location, methodName));
                        }

                        bool IsValidMember(ISymbol symbol)
                        {
                            if (symbol is IMethodSymbol delegatedMethod && delegatedMethod.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(knownTypes.FeatureCollection, delegatedMethod.ReturnType))
                            {
                                return true;
                            }

                            if (symbol is IFieldSymbol field && SymbolEqualityComparer.Default.Equals(knownTypes.FeatureCollection, field.Type))
                            {
                                return true;
                            }

                            if (symbol is IPropertySymbol property && SymbolEqualityComparer.Default.Equals(knownTypes.FeatureCollection, property.Type))
                            {
                                return true;
                            }

                            return false;
                        }
                    }
                }
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownTypes.KnownFeature))
                {
                    if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol contract)
                    {
                        var service = attribute.ConstructorArguments[1].Value as ISymbol ?? contract;

                        if (attribute.NamedArguments.FirstOrDefault(n => string.Equals("Factory", n.Key, StringComparison.Ordinal)).Value.Value is string methodName)
                        {
                            var members = method.ContainingType.GetMembers(methodName);

                            if (members.Length == 1 && members[0] is IMethodSymbol factory && factory.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(contract, factory.ReturnType))
                            {
                                service = factory;
                            }
                            else
                            {
                                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation();
                                context.ReportDiagnostic(Diagnostic.Create(InvalidFactoryMethod, location, methodName, contract.ToDisplayString()));
                                continue;
                            }
                        }

                        if (!contracts.Add(contract))
                        {
                            if (!features.Add((contract, service)))
                            {
                                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation();
                                context.ReportDiagnostic(Diagnostic.Create(DuplicateAttribute, location: location, service.ToDisplayString(), contract.ToDisplayString()));
                            }
                            else
                            {
                                features.Remove((contract, service));
                                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation();
                                context.ReportDiagnostic(Diagnostic.Create(SingleContractForFeature, location: location, service.ToDisplayString()));
                            }
                        }
                        else if (!features.Add((contract, service)))
                        {
                            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation();
                            context.ReportDiagnostic(Diagnostic.Create(DuplicateAttribute, location: location, service.ToDisplayString(), contract.ToDisplayString()));
                        }
                    }
                }
            }

            var source = method.Build(delegatedFeatures, features, isThreadSafe);

            context.AddSource($"{method.ContainingType.Name}_{method.Name}", source);
        }
    }
}
