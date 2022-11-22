// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Swick.DependencyInjection.Generator.Models;

internal abstract record Registration(TypeReference ServiceType)
{
    public virtual string VariableName => GetVariableName(ServiceType);

    protected string GetVariableName(TypeReference type) => $"_{type.Name}";
}
