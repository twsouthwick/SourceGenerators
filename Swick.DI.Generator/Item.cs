// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

internal record Item
{
    public string? Factory { get; init; }

    public TypeReference ImplementationType { get; init; }

    public string VariableName
    {
        get
        {
            var name = ImplementationType.FullName;
            var idx = name.LastIndexOf('.');
            return "_" + name.Substring(idx + 1);
        }
    }

    public TypeReference ServiceType { get; init; }
}
