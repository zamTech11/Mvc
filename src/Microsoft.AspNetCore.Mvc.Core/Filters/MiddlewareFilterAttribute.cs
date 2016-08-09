// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc.Core.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class MiddlewareFilterAttribute : Attribute, IFilterFactory, IOrderedFilter
    {
        public MiddlewareFilterAttribute(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            ImplementationType = type;
        }

        public Type ImplementationType { get; }

        public int Order { get; set; }

        public bool IsReusable { get; set; }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var middlewarePipelineService = serviceProvider.GetRequiredService<MiddlewareFilterBuilderService>();
            var pipeline = middlewarePipelineService.GetPipeline(ImplementationType);

            return new MiddlewareFilter(pipeline);
        }
    }
}
