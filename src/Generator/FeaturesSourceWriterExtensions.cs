// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Swick.Features.Generator.Models;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace Swick.Features.Generator;

internal static class FeaturesSourceWriterExtensions
{
    public static string Build(this ContainerRegistration registration)
    {
        var sb = new StringWriter();
        var indented = new IndentedTextWriter(sb);

        indented.WriteFileHeader();

        if (registration.Options.IsThreadSafe)
        {
            indented.WriteLine("using System.Threading;");
            indented.WriteLineNoTabs();
        }

        indented.Write("namespace ");
        indented.Write(registration.Details.Namespace);
        indented.WriteLine(";");

        indented.WriteLine();

        WriteContainingClass(indented, registration, registration.Details.TypeNames);

        return sb.ToString();
    }

    private static void WriteContainingClass(IndentedTextWriter indented, ContainerRegistration registration, ImmutableStack<Visible<TypeReference>> types)
    {
        if (types.IsEmpty)
        {
            WriteInjectedCode(indented, registration);
        }
        else
        {
            types = types.Pop(out var type);
            indented.Write(GetAccessibility(type.Visibility));
            indented.Write(" partial class ");
            indented.WriteLine(type.Item.Name);

            using (indented.AddBlock())
            {
                WriteContainingClass(indented, registration, types);
            }
        }
    }

    private static void WriteInjectedCode(IndentedTextWriter indented, ContainerRegistration registration)
    {
        if (!registration.Details.Errors.IsDefaultOrEmpty)
        {
            indented.WriteLine("// Invalid get method");
            return;
        }

        if (registration.Errors.IsEmpty && !registration.Registrations.IsDefaultOrEmpty)
        {
            foreach (var item in registration.Registrations)
            {
                indented.Write("private ");
                indented.Write(item.ServiceType);
                indented.Write("? ");
                indented.Write(item.VariableName);
                indented.WriteLine(";");
            }

            indented.WriteLineNoTabs();
        }

        indented.Write(GetAccessibility(registration.Details.Method.Visibility));
        indented.Write(" partial ");
        indented.Write(registration.Details.TypeParamName);
        indented.Write("? ");
        indented.Write(registration.Details.Method.Item.Name);
        indented.Write("<");
        indented.Write(registration.Details.TypeParamName);
        indented.WriteLine(">()");

        using (indented.AddBlock())
        {
            if (!registration.Errors.IsEmpty)
            {
                indented.WriteLine("return default;");
                return;
            }

            foreach (var item in registration.Registrations)
            {
                indented.Write("if (typeof(");
                indented.Write(registration.Details.TypeParamName);
                indented.Write(") == typeof(");
                indented.Write(item.ServiceType);
                indented.WriteLine("))");

                using (indented.AddBlock())
                {
                    WriteCreateIfNull(indented, item, registration.Options.IsThreadSafe);

                    indented.WriteLineNoTabs();
                    indented.Write("return (");
                    indented.Write(registration.Details.TypeParamName);
                    indented.Write(")");

                    if (item.ServiceType.TypeKind != TypeKind.Interface)
                    {
                        indented.Write("(object)");
                    }

                    indented.Write(item.VariableName);
                    indented.WriteLine(";");
                }

                indented.WriteLineNoTabs();
            }

            indented.WriteLine("return default;");
        }

        if (registration.Options.SetMethod is { } setMethod)
        {
            indented.WriteLineNoTabs();
            indented.Write(GetAccessibility(setMethod.Visibility));

            indented.Write(" partial");

            if (setMethod.Item.IsReturnable)
            {
                indented.Write(" bool ");
            }
            else
            {
                indented.Write(" void ");
            }

            indented.Write(setMethod.Item.Name);
            indented.Write("<");
            indented.Write(setMethod.Item.GenericParam);
            indented.Write(">(");
            indented.Write(setMethod.Item.GenericParam);
            indented.WriteLine("? feature)");

            var needsElse = false;

            using (indented.AddBlock())
            {
                foreach (var item in registration.Registrations)
                {
                    if (needsElse)
                    {
                        indented.Write("else ");
                    }
                    needsElse = true;

                    indented.Write("if (typeof(");
                    indented.Write(setMethod.Item.GenericParam);
                    indented.Write(") == typeof(");
                    indented.Write(item.ServiceType);
                    indented.WriteLine("))");

                    using (indented.AddBlock())
                    {
                        if (registration.Options.IsThreadSafe)
                        {
                            indented.Write("Interlocked.Exchange(ref ");
                            indented.Write(item.VariableName);
                            indented.Write(", (");
                        }
                        else
                        {
                            indented.Write(item.VariableName);
                            indented.Write(" = (");
                        }
                        indented.Write(item.ServiceType);
                        indented.Write("?)(object?)");
                        indented.Write("feature");

                        if (registration.Options.IsThreadSafe)
                        {
                            indented.WriteLine(");");
                        }
                        else
                        {
                            indented.WriteLine(";");
                        }

                        if (setMethod.Item.IsReturnable)
                        {
                            indented.WriteLine("return true;");
                        }
                    }
                }

                indented.WriteLine("else");

                using (indented.AddBlock())
                {
                    if (setMethod.Item.IsReturnable)
                    {
                        indented.WriteLine("return false;");
                    }
                    else
                    {
                        indented.WriteLine("throw new global::System.NotSupportedException();");
                    }

                }
            }
        }
    }

    private static void WriteCreateIfNull(IndentedTextWriter indented, Registration registration, bool isThreadSafe)
    {
        var variableName = registration.VariableName;
        indented.Write("if (");
        indented.Write(variableName);
        indented.WriteLine(" is null)");

        using (indented.AddBlock())
        {
            if (isThreadSafe)
            {
                indented.Write("Interlocked.CompareExchange(ref ");
                indented.Write(variableName);
                indented.Write(", ");
                indented.CreateInstance(registration);
                indented.WriteLine(", null);");
            }
            else
            {
                indented.Write(variableName);
                indented.Write(" = ");
                indented.CreateInstance(registration);
                indented.WriteLine(";");
            }
        }
    }

    private static void CreateInstance(this TextWriter writer, Registration registration)
    {
        if (registration is FactoryRegistration factory)
        {
            writer.Write(factory.Method.Name);
            writer.Write("()");
        }
        else if (registration is TypeRegistration type)
        {
            writer.Write("new ");
            writer.Write(type.ImplementationType.FullName);
            writer.Write("()");
        }
        else
        {
            writer.Write("null");
        }
    }

    private static void WriteSymbol(this TextWriter writer, ISymbol symbol)
    {
        writer.Write(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static string GetAccessibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Private => "private",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.Public => "public",
        _ => throw new NotImplementedException(),
    };
}
