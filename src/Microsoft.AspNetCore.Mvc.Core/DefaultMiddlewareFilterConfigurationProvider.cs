// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc
{
    public class DefaultMiddlewareFilterConfigurationProvider : IMiddlewareFilterConfigurationProvider
    {
        private readonly string _environmentName;

        public DefaultMiddlewareFilterConfigurationProvider(IHostingEnvironment hostingEnvironment)
        {
            _environmentName = hostingEnvironment.EnvironmentName;
        }

        public void Configure(Type middlewarePipelineProviderType, IApplicationBuilder applicationBuilder)
        {
            if (middlewarePipelineProviderType == null)
            {
                throw new ArgumentNullException(nameof(middlewarePipelineProviderType));
            }
            if (applicationBuilder == null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            var instance = Activator.CreateInstance(middlewarePipelineProviderType);
            var configureDelegateBuilder = GetConfigureDelegateBuilder(middlewarePipelineProviderType, _environmentName);
            var configureDelegate = configureDelegateBuilder.Build(instance);
            configureDelegate(applicationBuilder);
        }

        private static ConfigureBuilder GetConfigureDelegateBuilder(Type startupType, string environmentName)
        {
            var configureMethod = FindMethod(startupType, "Configure{0}", environmentName, typeof(void), required: true);
            return new ConfigureBuilder(configureMethod);
        }

        private static MethodInfo FindMethod(
            Type startupType,
            string methodName,
            string environmentName,
            Type returnType = null,
            bool required = true)
        {
            var methodNameWithEnv = string.Format(CultureInfo.InvariantCulture, methodName, environmentName);
            var methodNameWithNoEnv = string.Format(CultureInfo.InvariantCulture, methodName, "");

            var methods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithEnv)).ToList();
            if (selectedMethods.Count > 1)
            {
                throw new InvalidOperationException(
                    string.Format("Having multiple overloads of method '{0}' is not supported.", methodNameWithEnv));
            }
            if (selectedMethods.Count == 0)
            {
                selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithNoEnv)).ToList();
                if (selectedMethods.Count > 1)
                {
                    throw new InvalidOperationException(
                        string.Format("Having multiple overloads of method '{0}' is not supported.", methodNameWithNoEnv));
                }
            }

            var methodInfo = selectedMethods.FirstOrDefault();
            if (methodInfo == null)
            {
                if (required)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "A public method named '{0}' or '{1}' could not be found in the '{2}' type.",
                            methodNameWithEnv,
                            methodNameWithNoEnv,
                            startupType.FullName));

                }
                return null;
            }
            if (returnType != null && methodInfo.ReturnType != returnType)
            {
                if (required)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "The '{0}' method in the type '{1}' must have a return type of '{2}'.",
                            methodInfo.Name,
                            startupType.FullName,
                            returnType.Name));
                }
                return null;
            }
            return methodInfo;
        }

        private class ConfigureBuilder
        {
            public ConfigureBuilder(MethodInfo configure)
            {
                MethodInfo = configure;
            }

            public MethodInfo MethodInfo { get; }

            public Action<IApplicationBuilder> Build(object instance)
            {
                return (applicationBuilder) => Invoke(instance, applicationBuilder);
            }

            private void Invoke(object instance, IApplicationBuilder builder)
            {
                var serviceProvider = builder.ApplicationServices;
                var parameterInfos = MethodInfo.GetParameters();
                var parameters = new object[parameterInfos.Length];
                for (var index = 0; index < parameterInfos.Length; index++)
                {
                    var parameterInfo = parameterInfos[index];
                    if (parameterInfo.ParameterType == typeof(IApplicationBuilder))
                    {
                        parameters[index] = builder;
                    }
                    else
                    {
                        try
                        {
                            parameters[index] = serviceProvider.GetRequiredService(parameterInfo.ParameterType);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(string.Format(
                                "Could not resolve a service of type '{0}' for the parameter '{1}' of method '{2}' on type '{3}'.",
                                parameterInfo.ParameterType.FullName,
                                parameterInfo.Name,
                                MethodInfo.Name,
                                MethodInfo.DeclaringType.FullName), ex);
                        }
                    }
                }
                MethodInfo.Invoke(instance, parameters);
            }
        }
    }
}
