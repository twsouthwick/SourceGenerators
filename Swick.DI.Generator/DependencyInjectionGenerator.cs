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
                public RegisterFactoryAttribute(global::System.Type serviceType, global::System.String methodName)
                {
                }
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
}
