// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for resource filters, specifically <see cref="IResourceFilter.OnResourceExecuting"/> and
    /// <see cref="IAsyncResourceFilter.OnResourceExecutionAsync"/> calls.
    /// </summary>
    public abstract class ResourceExecutingContext : FilterContext
    {
        /// <summary>
        /// Gets or sets the result of the action to be executed.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="Result"/> to a non-<c>null</c> value inside a resource filter will
        /// short-circuit execution of additional resource filters and the action itself.
        /// </remarks>
        public abstract IActionResult Result { get; set; }
    }
}