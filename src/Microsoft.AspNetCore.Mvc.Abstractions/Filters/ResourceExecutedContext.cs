// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for resource filters, specifically <see cref="IResourceFilter.OnResourceExecuted"/> calls.
    /// </summary>
    public abstract class ResourceExecutedContext : FilterContext
    {
        /// <summary>
        /// Gets or sets a value which indicates whether or not execution was canceled by a resource filter.
        /// If true, then a resource filter short-circuted execution by setting
        /// <see cref="ResourceExecutingContext.Result"/>.
        /// </summary>
        public abstract bool Canceled { get; set; }

        /// <summary>
        /// Gets or set the current <see cref="Exception"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting <see cref="Exception"/> or <see cref="ExceptionDispatchInfo"/> to <c>null</c> will treat
        /// the exception as handled, and it will not be rethrown by the runtime.
        /// </para>
        /// <para>
        /// Setting <see cref="ExceptionHandled"/> to <c>true</c> will also mark the exception as handled.
        /// </para>
        /// </remarks>
        public abstract Exception Exception { get; set; }

        /// <summary>
        /// Gets or set the current <see cref="Exception"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting <see cref="Exception"/> or <see cref="ExceptionDispatchInfo"/> to <c>null</c> will treat
        /// the exception as handled, and it will not be rethrown by the runtime.
        /// </para>
        /// <para>
        /// Setting <see cref="ExceptionHandled"/> to <c>true</c> will also mark the exception as handled.
        /// </para>
        /// </remarks>
        public abstract ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets a value indicating whether or not the current <see cref="Exception"/> has been handled.
        /// </para>
        /// <para>
        /// If <c>false</c> the <see cref="Exception"/> will be rethrown by the runtime after resource filters
        /// have executed.
        /// </para>
        /// </summary>
        public abstract bool ExceptionHandled { get; set; }

        /// <summary>
        /// Gets or sets the result.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="Result"/> may be provided by execution of the action itself or by another
        /// filter.
        /// </para>
        /// <para>
        /// The <see cref="Result"/> has already been written to the response before being made available
        /// to resource filters.
        /// </para>
        /// </remarks>
        public abstract IActionResult Result { get; set; }
    }
}