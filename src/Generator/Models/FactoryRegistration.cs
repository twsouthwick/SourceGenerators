// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Swick.Features.Generator.Models;

internal record FactoryRegistration(TypeReference ServiceType, MethodReference Method) : Registration(ServiceType)
{
}
