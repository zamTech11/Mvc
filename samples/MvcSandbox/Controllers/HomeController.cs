// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace MvcSandbox.Controllers
{
    //[Route("{culture}/[controller]/[action]")] - works
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [MiddlewareFilter(typeof(Pipeline2))]
        public IActionResult Contact()
        {
            return View();
        }
    }

    [MiddlewareFilter(typeof(Pipeline2))]
    public class FooController : Controller
    {
        public IActionResult Index()
        {
            return Content("Foo.Index");
        }
    }

    [MiddlewareFilter(typeof(Pipeline1))]
    public class FilterConcatController : Controller
    {
        [MiddlewareFilter(typeof(Pipeline2))]
        public IActionResult Index()
        {
            return Content("FilterConcat.Index");
        }
    }
    public class Pipeline1
    {
        public void Configure(IApplicationBuilder applicationBuilder, IHostingEnvironment hostingEnvironment)
        {
            applicationBuilder.Use(async (httpContext, next) =>
            {
                Console.WriteLine("Pipeline1: Middleware1-Request");
                await next();
                Console.WriteLine("Pipeline1: Middleware1-Response");
            });

            applicationBuilder.Use(async (httpContext, next) =>
            {
                Console.WriteLine("Pipeline1: Middleware2-Request");
                await next();
                Console.WriteLine("Pipeline1: Middleware2-Response");
            });

            if (hostingEnvironment.EnvironmentName == "Development")
            {
                applicationBuilder.Use(async (httpContext, next) =>
                {
                    Console.WriteLine("Pipeline1: Middleware3-Request");
                    await next();
                    Console.WriteLine("Pipeline1: Middleware3-Response");
                });
            }
        }
    }

    public class Pipeline2
    {
        public void Configure(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.Use(async (httpContext, next) =>
            {
                Console.WriteLine("Pipeline2: Middleware1-Request");
                await next();
                Console.WriteLine("Pipeline2: Middleware1-Response");
            });
        }
    }

    public class LocalizationPipeline
    {
        public void Configure(IApplicationBuilder appBuilder)
        {
            var locOptions = appBuilder.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            appBuilder.UseRequestLocalization(locOptions.Value);

        }
    }
}
