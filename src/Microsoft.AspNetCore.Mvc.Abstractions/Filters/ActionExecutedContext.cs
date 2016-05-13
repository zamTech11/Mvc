// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for action filters, specifically <see cref="IActionFilter.OnActionExecuted"/> calls.
    /// </summary>
    public abstract class ActionExecutedContext : FilterContext
    {
        /// <summary>
        /// Gets or sets an indication that an action filter short-circuited the action and the action filter pipeline.
        /// </summary>
        public abstract bool Canceled { get; set; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public abstract object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="System.Exception"/> caught while executing the action or action filters, if
        /// any.
        /// </summary>
        public abstract Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> for the
        /// <see cref="Exception"/>, if an <see cref="System.Exception"/> was caught and this information captured.
        /// </summary>
        public virtual ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

        /// <summary>
        /// Gets or sets an indication that the <see cref="Exception"/> has been handled.
        /// </summary>
        public abstract bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/>.
        /// </summary>
        public abstract IActionResult Result { get; set; }
    }
}
