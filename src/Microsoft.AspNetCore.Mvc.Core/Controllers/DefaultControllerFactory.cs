// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Core;

namespace Microsoft.AspNetCore.Mvc.Controllers
{
    /// <summary>
    /// Default implementation for <see cref="IControllerFactory"/>.
    /// </summary>
    public class DefaultControllerFactory : IControllerFactory
    {
        private readonly IControllerActivator _controllerActivator;
        private readonly IControllerPropertyActivator[] _propertyActivators;

        /// <summary>
        /// Initializes a new instance of <see cref="DefaultControllerFactory"/>.
        /// </summary>
        /// <param name="controllerActivator">
        /// <see cref="IControllerActivator"/> used to create controller instances.
        /// </param>
        /// <param name="propertyActivators">
        /// A set of <see cref="IControllerPropertyActivator"/> instances used to initialize controller
        /// properties.
        /// </param>
        public DefaultControllerFactory(
            IControllerActivator controllerActivator,
            IEnumerable<IControllerPropertyActivator> propertyActivators)
        {
            if (controllerActivator == null)
            {
                throw new ArgumentNullException(nameof(controllerActivator));
            }

            if (propertyActivators == null)
            {
                throw new ArgumentNullException(nameof(propertyActivators));
            }

            _controllerActivator = controllerActivator;
            _propertyActivators = propertyActivators.ToArray();
        }

        /// <summary>
        /// The <see cref="IControllerActivator"/> used to create a controller.
        /// </summary>
        protected IControllerActivator ControllerActivator
        {
            get
            {
                return _controllerActivator;
            }
        }

        public Func<ControllerContext, object> CreateControllerDelegate(ControllerActionDescriptor actionDescriptor)
        {
            var activator = _controllerActivator.CreateDelegate(actionDescriptor);

            var setters = new List<Action<ControllerContext, object>>();
            for (var i = 0; i < _propertyActivators.Length; i++)
            {
                setters.Add(_propertyActivators[0].Activate(actionDescriptor));
            }

            return (controllerContext) =>
            {
                var controller = activator(controllerContext);
                for (var i = 0; i < setters.Count; i++)
                {
                    setters[0](controllerContext, controller);
                }

                return controller;
            };
        }

        public Action<ControllerContext, object> ReleaseControllerDelegate(ControllerActionDescriptor actionDescriptor)
        {
            return _controllerActivator.ReleaseDelegate(actionDescriptor);
        }
    }
}
