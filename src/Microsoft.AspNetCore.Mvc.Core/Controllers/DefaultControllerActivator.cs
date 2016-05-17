// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc.Controllers
{
    /// <summary>
    /// <see cref="IControllerActivator"/> that uses type activation to create controllers.
    /// </summary>
    public class DefaultControllerActivator : IControllerActivator
    {
        private readonly ITypeActivatorCache _typeActivatorCache;

        /// <summary>
        /// Creates a new <see cref="DefaultControllerActivator"/>.
        /// </summary>
        /// <param name="typeActivatorCache">The <see cref="ITypeActivatorCache"/>.</param>
        public DefaultControllerActivator(ITypeActivatorCache typeActivatorCache)
        {
            if (typeActivatorCache == null)
            {
                throw new ArgumentNullException(nameof(typeActivatorCache));
            }

            _typeActivatorCache = typeActivatorCache;
        }

        public Func<ControllerContext, object> CreateDelegate(ControllerActionDescriptor actionDescriptor)
        {
            if (actionDescriptor == null)
            {
                throw new ArgumentNullException(nameof(actionDescriptor));
            }

            var controllerTypeInfo = actionDescriptor.ControllerTypeInfo;
            if (controllerTypeInfo == null)
            {
                throw new ArgumentException(Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(ControllerActionDescriptor.ControllerTypeInfo),
                    nameof(ControllerActionDescriptor)));
            }

            var constructors = controllerTypeInfo.GetConstructors();
            if (constructors.Length == 1 && constructors[0].GetParameters().Length == 0)
            {
                return 
                    Expression.Lambda<Func<ControllerContext, object>>(
                        Expression.New(constructors[0]),
                        Expression.Parameter(typeof(ControllerContext), "controllerContext"))
                    .Compile();
            }
            else
            {
                var factory = ActivatorUtilities.CreateFactory(controllerTypeInfo.AsType(), Type.EmptyTypes);

                return (controllerContext) =>
                {
                    var services = controllerContext.HttpContext.RequestServices;
                    return factory(services, null);
                };
            }
        }

        public Action<ControllerContext, object> ReleaseDelegate(ControllerActionDescriptor actionDescriptor)
        {
            return (controllerContext, controller) => { (controller as IDisposable)?.Dispose(); };
        }
    }
}
