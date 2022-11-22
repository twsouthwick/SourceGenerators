﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Swick.DependencyInjection.Generator.Models;

internal record ContainerRegistration(ContainerDetails Details)
{
    public ContainerOptions Options { get; init; } = new();

    public ImmutableArray<Registration> Registrations { get; init; }
}
