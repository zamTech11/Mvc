// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for result filters, specifically <see cref="IResultFilter.OnResultExecuting"/> and
    /// <see cref="IAsyncResultFilter.OnResultExecutionAsync"/> calls.
    /// </summary>
    public abstract class ResultExecutingContext : FilterContext
    {
        /// <summary>
        /// Gets or sets an indication the result filter pipeline should be short-circuited.
        /// </summary>
        public abstract bool Cancel { get; set; }

        /// <summary>
        /// Gets the controller instance containing the action.
        /// </summary>
        public abstract  object Controller { get; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/> to execute. Setting <see cref="Result"/> to a non-<c>null</c>
        /// value inside a result filter will short-circuit the result and any remaining result filters.
        /// </summary>
        public abstract IActionResult Result { get; set; }
    }
}
