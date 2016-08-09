// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.AspNetCore.Mvc
{
    public interface IMiddlewareFilterConfigurationProvider
    {
        void Configure(Type middlewarePipelineProviderType, IApplicationBuilder applicationBuilder);
    }
}
