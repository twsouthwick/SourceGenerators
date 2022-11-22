// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator.Models;

internal record struct MethodReference(string Name);

internal record struct TypeName(string FullName);

internal record struct TypeReference(string FullName, ImmutableArray<TypeName> Parameters, TypeKind TypeKind)
{
    private static readonly char[] _delimiters = new char[] { '.', ':' };

    public string Name
    {
        get
        {
            var name = FullName;
            var idx = name.LastIndexOfAny(_delimiters);

            return idx < 0 ? FullName : FullName.Substring(idx + 1);
        }
    }

    public override string ToString() => FullName;
}
