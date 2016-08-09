// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc
{
    public class DefaultMiddlewareFilterConfigurationProviderTest
    {
        [Fact]
        public void ValidConfigure_WithNoEnvironment_DoesNotThrow()
        {
            // Arrange
            var hostingEnvironment = GetHostingEnvironment();
            var services = new ServiceCollection();
            services.AddSingleton(hostingEnvironment);
            var applicationBuilder = GetApplicationBuilder(services);
            var provider = new DefaultMiddlewareFilterConfigurationProvider(hostingEnvironment);

            // Act & Assert
            provider.Configure(typeof(ValidConfigure_WithNoEnvironment), applicationBuilder);
        }

        [Fact]
        public void ValidConfigure_WithNoEnvironment_AndAdditionalServices_DoesNotThrow()
        {
            // Arrange
            var hostingEnvironment = GetHostingEnvironment();
            var loggerFactory = Mock.Of<ILoggerFactory>();
            var services = new ServiceCollection();
            services.AddSingleton(hostingEnvironment);
            services.AddSingleton(loggerFactory);
            var applicationBuilder = GetApplicationBuilder(services);
            var provider = new DefaultMiddlewareFilterConfigurationProvider(hostingEnvironment);

            // Act & Assert
            provider.Configure(typeof(ValidConfigure_WithNoEnvironment_AdditionalServices), applicationBuilder);
        }

        [Fact]
        public void ValidConfigure_WithEnvironment_DoesNotThrow()
        {
            // Arrange
            var hostingEnvironment = GetHostingEnvironment("Production");
            var services = new ServiceCollection();
            services.AddSingleton(hostingEnvironment);
            var applicationBuilder = GetApplicationBuilder(services);
            var provider = new DefaultMiddlewareFilterConfigurationProvider(hostingEnvironment);

            // Act & Assert
            provider.Configure(typeof(ValidConfigure_WithEnvironment), applicationBuilder);
        }

        [Fact]
        public void ValidConfigure_WithEnvironment_AndAdditionalServices_DoesNotThrow()
        {
            // Arrange
            var hostingEnvironment = GetHostingEnvironment("Production");
            var services = new ServiceCollection();
            services.AddSingleton(hostingEnvironment);
            services.AddSingleton(Mock.Of<ILoggerFactory>());
            var applicationBuilder = GetApplicationBuilder(services);
            var provider = new DefaultMiddlewareFilterConfigurationProvider(hostingEnvironment);

            // Act & Assert
            provider.Configure(typeof(ValidConfigure_WithEnvironment_AdditionalServices), applicationBuilder);
        }

        [Fact]
        public void InvalidType_NoConfigure_Throws()
        {
            // Arrange
            var type = typeof(InvalidType_NoConfigure);
            var hostingEnvironment = GetHostingEnvironment();
            var services = new ServiceCollection();
            services.AddSingleton(hostingEnvironment);
            var applicationBuilder = GetApplicationBuilder(services);
            var provider = new DefaultMiddlewareFilterConfigurationProvider(hostingEnvironment);
            var expected = $"A public method named 'ConfigureDevelopment' or 'Configure' could not be found in the " +
                $"'{type.FullName}' type.";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                provider.Configure(type, applicationBuilder);
            });
            Assert.Equal(expected, exception.Message);
        }

        [Fact]
        public void InvalidType_NoPublicConfigure_Throws()
        {
            // Arrange
            var type = typeof(InvalidType_NoPublic_Configure);
            var hostingEnvironment = GetHostingEnvironment();
            var services = new ServiceCollection();
            services.AddSingleton(hostingEnvironment);
            var applicationBuilder = GetApplicationBuilder(services);
            var provider = new DefaultMiddlewareFilterConfigurationProvider(hostingEnvironment);
            var expected = $"A public method named 'ConfigureDevelopment' or 'Configure' could not be found in the " +
                $"'{type.FullName}' type.";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                provider.Configure(type, applicationBuilder);
            });
            Assert.Equal(expected, exception.Message);
        }

        private IHostingEnvironment GetHostingEnvironment(string environmentName = "Development")
        {
            var hostingEnvironment = new Mock<IHostingEnvironment>();
            hostingEnvironment.SetupGet(he => he.EnvironmentName).Returns(environmentName);
            return hostingEnvironment.Object;
        }

        private IApplicationBuilder GetApplicationBuilder(ServiceCollection services = null)
        {
            if (services == null)
            {
                services = new ServiceCollection();
            }
            var serviceProvider = services.BuildServiceProvider();

            var applicationBuilder = new Mock<IApplicationBuilder>();
            applicationBuilder
                .SetupGet(a => a.ApplicationServices)
                .Returns(serviceProvider);

            return applicationBuilder.Object;
        }

        private class ValidConfigure_WithNoEnvironment
        {
            public void Configure(IApplicationBuilder appBuilder) { }
        }

        private class ValidConfigure_WithNoEnvironment_AdditionalServices
        {
            public void Configure(
                IApplicationBuilder appBuilder,
                IHostingEnvironment hostingEnvironment,
                ILoggerFactory loggerFactory)
            {
                if (hostingEnvironment == null)
                {
                    throw new ArgumentNullException(nameof(hostingEnvironment));
                }
                if (loggerFactory == null)
                {
                    throw new ArgumentNullException(nameof(loggerFactory));
                }
            }
        }

        private class ValidConfigure_WithEnvironment
        {
            public void ConfigureProduction(IApplicationBuilder appBuilder) { }
        }

        private class ValidConfigure_WithEnvironment_AdditionalServices
        {
            public void ConfigureProduction(
                IApplicationBuilder appBuilder,
                IHostingEnvironment hostingEnvironment,
                ILoggerFactory loggerFactory)
            {
                if (hostingEnvironment == null)
                {
                    throw new ArgumentNullException(nameof(hostingEnvironment));
                }
                if (loggerFactory == null)
                {
                    throw new ArgumentNullException(nameof(loggerFactory));
                }
            }
        }

        private class MultipleConfigureWithEnvironments
        {
            public void ConfigureDevelopment(IApplicationBuilder appBuilder)
            {

            }

            public void ConfigureProduction(IApplicationBuilder appBuilder)
            {

            }
        }

        private class InvalidConfigure_NoParameters
        {
            public void Configure()
            {

            }
        }

        private class InvalidType_NoConfigure
        {
            public void Foo(IApplicationBuilder appBuilder)
            {

            }
        }

        private class InvalidType_NoPublic_Configure
        {
            private void Configure(IApplicationBuilder appBuilder)
            {

            }
        }
    }
}
