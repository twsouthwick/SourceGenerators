// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Swick.Features.Generator.Models;

internal record ContainerDetails(string Namespace, ImmutableStack<Visible<TypeReference>> TypeNames, Visible<MethodReference> Method, string TypeParamName)
{
}
