// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Swick.Features.Generator.Models;

internal record ContainerOptions
{
    public bool IsThreadSafe { get; init; }

    public Visible<SetMethodInfo>? SetMethod { get; init; }
}

internal record SetMethodInfo(string Name, bool IsReturnable, string GenericParam)
{
}
