// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Swick.DependencyInjection.Generator.Models;

internal record TypeRegistration(TypeReference ServiceType) : Registration(ServiceType)
{
    public TypeReference ImplementationType { get; init; }

    public override string VariableName => GetVariableName(ImplementationType);
}
