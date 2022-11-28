﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

namespace Swick.Features.Generator;

[Generator]
public class FeaturesGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DuplicateAttribute = new(KnownErrors.DuplicateService, "Duplicate known registration", "Service {0} is already registered for {1}", "Features", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidFactoryMethod = new(KnownErrors.InvalidFactory, "Invalid factory method", "Method must have no parameters and return service type", "Features", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidGetMethod = new(KnownErrors.InvalidGenericGetMethod, "Invalid feature get method", "Get method must have no parameters, a single generic parameter as its return type.", "Features", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidSetMethod = new(KnownErrors.InvalidSetMethod, "Invalid feature get method", "Set method must have single generic parameter and an optional boolean return.", "Features", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrations = context.GetContainerRegistrations();

        context.RegisterSourceOutput(registrations, static (context, registrations) =>
        {
            var typeNames = string.Join(".", registrations.Details.TypeNames.Select(t => t.Item.Name));
            var fileName = $"{registrations.Details.Namespace}.{typeNames}.{registrations.Details.Method.Item.Name}";
            var source = registrations.Build();

            context.AddSource(fileName, source);

            foreach (var error in registrations.Errors)
            {
                var descriptor = error.Id switch
                {
                    KnownErrors.InvalidFactory => InvalidFactoryMethod,
                    KnownErrors.DuplicateService => DuplicateAttribute,
                    KnownErrors.InvalidGenericGetMethod => InvalidGetMethod,
                    KnownErrors.InvalidSetMethod => InvalidSetMethod,
                    _ => throw new NotImplementedException(),
                };

                context.ReportDiagnostic(Diagnostic.Create(descriptor, error.Location, messageArgs: error.MessageArgs));
            }
        });

        context.RegisterPostInitializationOutput(context =>
        {
            var sb = new StringWriter();
            var indented = new IndentedTextWriter(sb);

            const string Source = """
            // <auto-generated />

            #nullable enable

            namespace Swick.Features;

            [global::System.Diagnostics.Conditional("SWICK_FEATURES")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
            internal sealed class RegisterAttribute : global::System.Attribute
            {
                public RegisterAttribute(global::System.Type contract, global::System.Type? service = null)
                {
                }
            }
            
            [global::System.Diagnostics.Conditional("SWICK_FEATURES")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
            internal sealed class RegisterFactoryAttribute : global::System.Attribute
            {
                public RegisterFactoryAttribute(global::System.Type serviceType, global::System.String methodName)
                {
                }
            }
            
            [global::System.Diagnostics.Conditional("SWICK_FEATURES")]
            [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
            internal sealed class ContainerOptionsAttribute : global::System.Attribute
            {
                public bool IsThreadSafe { get; set; }

                public string? SetMethod { get; set; }
            }

            """;

            context.AddSource("FeaturesRegistrationAttributes", Source);
        });
    }
}
