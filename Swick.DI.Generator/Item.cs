// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

internal record Item
{
    public ImmutableArray<TypeReference> Args { get; init; }

    public string? Factory { get; init; }

    public TypeReference Name { get; init; }

    public TypeReference Type { get; init; }
}
