// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class DefaultControllerPropertyActivator : IControllerPropertyActivator
    {
        private readonly ConcurrentDictionary<Type, PropertyActivator<ControllerContext>[]> _activateActions;
        private readonly Func<Type, PropertyActivator<ControllerContext>[]> _getPropertiesToActivate;

        public DefaultControllerPropertyActivator()
        {
            _activateActions = new ConcurrentDictionary<Type, PropertyActivator<ControllerContext>[]>();
            _getPropertiesToActivate = GetPropertiesToActivate;
        }

        public Action<ControllerContext, object> Activate(ControllerActionDescriptor actionDescriptor)
        {
            var controllerType = actionDescriptor.ControllerTypeInfo.AsType();
            var propertiesToActivate = _activateActions.GetOrAdd(
                controllerType,
                _getPropertiesToActivate);

            var activator = propertiesToActivate.Last();

            return (controllerContext, controller) => { activator.Activate(controller, controllerContext); };
        }

        private PropertyActivator<ControllerContext>[] GetPropertiesToActivate(Type type)
        {
            IEnumerable<PropertyActivator<ControllerContext>> activators;
            activators = PropertyActivator<ControllerContext>.GetPropertiesToActivate(
                type,
                typeof(ActionContextAttribute),
                p => new PropertyActivator<ControllerContext>(p, c => c));

            activators = activators.Concat(PropertyActivator<ControllerContext>.GetPropertiesToActivate(
                type,
                typeof(ControllerContextAttribute),
                p => new PropertyActivator<ControllerContext>(p, c => c)));

            return activators.ToArray();
        }
    }
}
