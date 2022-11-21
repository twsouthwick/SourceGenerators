// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Swick.DependencyInjection.Generator;

internal record KnownTypes
{
    public INamedTypeSymbol? KnownFeature { get; init; }

    public INamedTypeSymbol? ThreadSafe { get; init; }

    public INamedTypeSymbol? Delegated { get; init; }

    public INamedTypeSymbol? FeatureCollection { get; init; }

    [MemberNotNullWhen(true, nameof(KnownFeature), nameof(ThreadSafe), nameof(Delegated), nameof(FeatureCollection))]
    public bool IsAvailable => KnownFeature is not null && ThreadSafe is not null && Delegated is not null && FeatureCollection is not null;
}
