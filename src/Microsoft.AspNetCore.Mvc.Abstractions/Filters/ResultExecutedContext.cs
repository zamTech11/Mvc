// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for result filters, specifically <see cref="IResultFilter.OnResultExecuted"/> calls.
    /// </summary>
    public abstract class ResultExecutedContext : FilterContext
    {
        /// <summary>
        /// Gets or sets an indication that a result filter set <see cref="ResultExecutingContext.Cancel"/> to
        /// <c>true</c> and short-circuited the filter pipeline.
        /// </summary>
        public abstract bool Canceled { get; set; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public abstract object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="System.Exception"/> caught while executing the result or result filters, if
        /// any.
        /// </summary>
        public abstract Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> for the
        /// <see cref="Exception"/>, if an <see cref="System.Exception"/> was caught and this information captured.
        /// </summary>
        public abstract ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

        /// <summary>
        /// Gets or sets an indication that the <see cref="Exception"/> has been handled.
        /// </summary>
        public abstract bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets the <see cref="IActionResult"/> copied from <see cref="ResultExecutingContext.Result"/>.
        /// </summary>
        public abstract IActionResult Result { get; }
    }
}
