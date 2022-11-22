// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator;

internal record Visible<T>(T Item, Accessibility Visibility);

internal record ContainerDetails(string Namespace, ImmutableStack<Visible<TypeReference>> TypeNames, Visible<MethodReference> Method)
{
}

internal record ContainerRegistration(ContainerDetails Details)
{
    public ContainerOptions Options { get; init; } = new();

    public ImmutableArray<Item> Items { get; init; }
}
