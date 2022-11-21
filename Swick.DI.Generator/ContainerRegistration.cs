// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

internal record ContainerRegistration
{
    public ContainerOptions Options { get; init; } = new();

    public ImmutableArray<Item> Items { get; init; }
}
