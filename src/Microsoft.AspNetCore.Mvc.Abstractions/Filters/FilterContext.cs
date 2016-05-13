// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// An abstract context for filters.
    /// </summary>
    public abstract class FilterContext
    {
        /// <summary>
        /// Gets the <see cref="Mvc.ActionContext"/>.
        /// </summary>
        public abstract ActionContext ActionContext { get; }

        /// <summary>
        /// Gets the <see cref="Abstractions.ActionDescriptor"/>
        /// </summary>
        public virtual ActionDescriptor ActionDescriptor => ActionContext.ActionDescriptor;

        /// <summary>
        /// Gets all applicable <see cref="IFilterMetadata"/> implementations.
        /// </summary>
        public abstract IList<IFilterMetadata> Filters { get; }

        /// <summary>
        /// Gets the <see cref="Http.HttpContext"/>.
        /// </summary>
        public virtual HttpContext HttpContext => ActionContext.HttpContext;

        /// <summary>
        /// Gets the <see cref="ModelStateDictionary"/>.
        /// </summary>
        public virtual ModelStateDictionary ModelState => ActionContext.ModelState;

        /// <summary>
        /// Gets the <see cref="AspNetCore.Routing.RouteData"/>.
        /// </summary>
        public virtual RouteData RouteData => ActionContext.RouteData;
    }
}
