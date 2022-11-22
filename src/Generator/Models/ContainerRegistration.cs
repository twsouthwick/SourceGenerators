// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Swick.Features.Generator.Models;

internal record ContainerRegistration
{
    public ContainerRegistration(ContainerDetails details)
    {
        Details = details;
        Errors = details.Errors;
    }

    public ContainerDetails Details { get; init; }

    public ContainerOptions Options { get; init; } = new();

    public ImmutableArray<Registration> Registrations { get; init; }

    public ImmutableArray<Error> Errors { get; init; }
}

internal record Error(string Id, Location? Location, params string[] MessageArgs);
