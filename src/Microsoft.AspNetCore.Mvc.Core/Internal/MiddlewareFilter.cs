// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class MiddlewareFilter : IAsyncResourceFilter, IOrderedFilter
    {
        private readonly RequestDelegate _requestDelegate;

        public MiddlewareFilter(RequestDelegate requestDelegate)
        {
            if (requestDelegate == null)
            {
                throw new ArgumentNullException(nameof(requestDelegate));
            }

            _requestDelegate = requestDelegate;
        }

        public int Order
        {
            get
            {
                return int.MinValue + 100;
            }
        }

        public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            var feature = new MiddlewareFilterFeature()
            {
                ResourceExecutionDelegate = next,
                ResourceExecutingContext = context
            };
            context.HttpContext.Features.Set<IMiddlewareFilterFeature>(feature);

            // TODO: middleware pipeline could throw exceptions
            return _requestDelegate(httpContext);
        }
    }
}
