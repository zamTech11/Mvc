// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Mvc.Controllers
{
    /// <summary>
    /// Provides methods to create a controller.
    /// </summary>
    public interface IControllerActivator
    {
        Func<ControllerContext, object> CreateDelegate(ControllerActionDescriptor actionDescriptor);

        Action<ControllerContext, object> ReleaseDelegate(ControllerActionDescriptor actionDescriptor);
    }
}