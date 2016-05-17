// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Mvc.Controllers
{
    /// <summary>
    /// Provides methods for creation and disposal of controllers.
    /// </summary>
    public interface IControllerFactory
    {
        Func<ControllerContext, object> CreateControllerDelegate(ControllerActionDescriptor actionDescriptor);

        Action<ControllerContext, object> ReleaseControllerDelegate(ControllerActionDescriptor actionDescriptor);
    }
}
