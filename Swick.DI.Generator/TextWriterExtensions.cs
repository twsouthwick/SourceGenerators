﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CodeDom.Compiler;

namespace Swick.DependencyInjection.Generator;

public static class TextWriterExtensions
{
    public static void WriteFileHeader(this TextWriter writer)
    {
        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine();
        writer.WriteLine("#nullable enable");
        writer.WriteLine();
    }

    public static void WriteLineNoTabs(this IndentedTextWriter writer) => writer.WriteLineNoTabs(string.Empty);

    public static Indentation AddBlock(this IndentedTextWriter writer, BlockOptions? options = null)
    {
        writer.WriteLine("{");
        var finalText = options?.FinalText is null ? "}" : options.FinalText + "}";

        return new(writer, options is null ? new() { FinalText = finalText } : options with { FinalText = finalText });
    }
}
