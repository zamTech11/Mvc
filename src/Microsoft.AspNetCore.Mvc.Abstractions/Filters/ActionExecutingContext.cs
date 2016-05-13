// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for action filters, specifically <see cref="IActionFilter.OnActionExecuted"/> and
    /// <see cref="IAsyncActionFilter.OnActionExecutionAsync"/> calls.
    /// </summary>
    public abstract class ActionExecutingContext : FilterContext
    {
        /// <summary>
        /// Gets the arguments to pass when invoking the action. Keys are parameter names.
        /// </summary>
        public abstract IDictionary<string, object> ActionArguments { get; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public abstract object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/> to execute. Setting <see cref="Result"/> to a non-<c>null</c>
        /// value inside an action filter will short-circuit the action and any remaining action filters.
        /// </summary>
        public abstract IActionResult Result { get; set; }
    }
}
