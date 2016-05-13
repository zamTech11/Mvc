// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// A context for exception filters i.e. <see cref="IExceptionFilter"/> and
    /// <see cref="IAsyncExceptionFilter"/> implementations.
    /// </summary>
    public abstract class ExceptionContext : FilterContext
    {
        /// <summary>
        /// Gets or sets the <see cref="System.Exception"/> caught while executing the action.
        /// </summary>
        public abstract Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="System.Runtime.ExceptionServices.ExceptionDispatchInfo"/> for the
        /// <see cref="Exception"/>, if this information was captured.
        /// </summary>
        public abstract ExceptionDispatchInfo ExceptionDispatchInfo { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IActionResult"/>.
        /// </summary>
        public abstract IActionResult Result { get; set; }
    }
}
