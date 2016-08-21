// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// Executes a middleware pipeline provided the by the <see cref="MiddlewareFilterAttribute.PipelineConfiguringType"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class MiddlewareFilterAttribute : Attribute, IFilterFactory, IOrderedFilter
    {
        /// <summary>
        /// Instantiates a new instance of <see cref="MiddlewareFilterAttribute"/>.
        /// </summary>
        /// <param name="pipelineConfiguringType">A type which configures a middleware pipeline</param>
        public MiddlewareFilterAttribute(Type pipelineConfiguringType)
        {
            if (pipelineConfiguringType == null)
            {
                throw new ArgumentNullException(nameof(pipelineConfiguringType));
            }

            PipelineConfiguringType = pipelineConfiguringType;
        }

        public Type PipelineConfiguringType { get; }

        /// <inheritdoc />
        public int Order { get; set; }

        /// <inheritdoc />
        public bool IsReusable { get; } = true;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var middlewarePipelineService = serviceProvider.GetRequiredService<MiddlewareFilterBuilderService>();
            var pipeline = middlewarePipelineService.GetPipeline(PipelineConfiguringType);

            return new MiddlewareFilter(pipeline);
        }
    }
}
