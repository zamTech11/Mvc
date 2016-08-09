// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    /// <summary>
    /// Builds a middleware pipeline after receiving the pipeline from a pipeline provider
    /// </summary>
    public class MiddlewareFilterBuilderService
    {
        private readonly ConcurrentDictionary<Type, Lazy<RequestDelegate>> _pipelinesCache
            = new ConcurrentDictionary<Type, Lazy<RequestDelegate>>();
        private readonly IMiddlewareFilterConfigurationProvider _middlewareFilterConfigurationProvider;

        public IApplicationBuilder ApplicationBuilder { get; set; }

        public MiddlewareFilterBuilderService(IMiddlewareFilterConfigurationProvider middlewareFilterConfigurationProvider)
        {
            _middlewareFilterConfigurationProvider = middlewareFilterConfigurationProvider;
        }

        public RequestDelegate GetPipeline(Type middlewarePipelineProviderType)
        {
            // Build the pipeline only once. This is similar to how middlewares are used where they are constructed
            // only once.

            var requestDelegate = _pipelinesCache.GetOrAdd(
                middlewarePipelineProviderType,
                key => new Lazy<RequestDelegate>(() => BuildPipeline(key)));

            return requestDelegate.Value;
        }

        private RequestDelegate BuildPipeline(Type middlewarePipelineProviderType)
        {
            var nestedAppBuilder = ApplicationBuilder.New();

            // Get the user provided pipeline
            _middlewareFilterConfigurationProvider.Configure(middlewarePipelineProviderType, nestedAppBuilder);

            // Attach a middleware in the end so that it continues the execution of rest of the MVC filter pipeline
            nestedAppBuilder.Run(async (httpContext) =>
            {
                var feature = httpContext.Features.Get<IMiddlewareFilterFeature>();
                var resourceExecutionDelegate = feature.ResourceExecutionDelegate;

                var resourceExecutedContext = await resourceExecutionDelegate();
                if (resourceExecutedContext.Exception != null)
                {
                    throw resourceExecutedContext.Exception;
                }
            });

            return nestedAppBuilder.Build();
        }
    }
}
