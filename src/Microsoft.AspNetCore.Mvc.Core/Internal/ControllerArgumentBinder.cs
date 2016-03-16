// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    /// <summary>
    /// Provides a default implementation of <see cref="IControllerActionArgumentBinder"/>.
    /// Uses ModelBinding to populate action parameters.
    /// </summary>
    public class ControllerArgumentBinder : IControllerActionArgumentBinder
    {
        private static readonly MethodInfo CallPropertyAddRangeOpenGenericMethod =
            typeof(ControllerArgumentBinder).GetTypeInfo().GetDeclaredMethod(
                nameof(CallPropertyAddRange));

        private readonly ModelBinderManager _bro;
        private readonly IModelMetadataProvider _modelMetadataProvider;
        private readonly IObjectModelValidator _validator;

        public ControllerArgumentBinder(
            ModelBinderManager bro,
            IModelMetadataProvider modelMetadataProvider,
            IObjectModelValidator validator)
        {
            _modelMetadataProvider = modelMetadataProvider;
            _validator = validator;
        }

        public async Task<IDictionary<string, object>> BindActionArgumentsAsync(
            ControllerContext context,
            object controller)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (context.ActionDescriptor == null)
            {
                throw new ArgumentException(Resources.FormatPropertyOfTypeCannotBeNull(
                    nameof(ControllerContext.ActionDescriptor),
                    nameof(ControllerContext)));
            }

            var broContext = new ModelBinderManagerContext();
            broContext.Metadata = new ActionDescriptorBroMetadata(
                _modelMetadataProvider, 
                context.ActionDescriptor);

            var binder = _bro.GetItBro(broContext);

            var bindingContext = DefaultModelBindingContext.CreateBindingContext(c1, null, null, null);
            bindingContext.Model = context;

            await binder.BindModelAsync(bindingContext);
            return context.Arguments;
        }

        // Called via reflection.
        private static void CallPropertyAddRange<TElement>(object target, object source)
        {
            var targetCollection = (ICollection<TElement>)target;
            var sourceCollection = source as IEnumerable<TElement>;
            if (sourceCollection != null && !targetCollection.IsReadOnly)
            {
                targetCollection.Clear();
                foreach (var item in sourceCollection)
                {
                    targetCollection.Add(item);
                }
            }
        }

        private void ActivateProperties(
            ControllerActionDescriptor actionDescriptor,
            object controller,
            IDictionary<string, object> properties)
        {
            var propertyHelpers = PropertyHelper.GetProperties(controller);
            for (var i = 0; i < actionDescriptor.BoundProperties.Count; i++)
            {
                var property = actionDescriptor.BoundProperties[i];

                PropertyHelper propertyHelper = null;
                for (var j = 0; j < propertyHelpers.Length; j++)
                {
                    if (string.Equals(propertyHelpers[j].Name, property.Name, StringComparison.Ordinal))
                    {
                        propertyHelper = propertyHelpers[j];
                        break;
                    }
                }

                object value;
                if (propertyHelper == null || !properties.TryGetValue(property.Name, out value))
                {
                    continue;
                }

                var propertyType = propertyHelper.Property.PropertyType;
                var metadata = _modelMetadataProvider.GetMetadataForType(propertyType);

                if (propertyHelper.Property.CanWrite && propertyHelper.Property.SetMethod?.IsPublic == true)
                {
                    // Handle settable property. Do not set the property to null if the type is a non-nullable type.
                    if (value != null || metadata.IsReferenceOrNullableType)
                    {
                        propertyHelper.SetValue(controller, value);
                    }

                    continue;
                }

                if (propertyType.IsArray)
                {
                    // Do not attempt to copy values into an array because an array's length is immutable. This choice
                    // is also consistent with MutableObjectModelBinder's handling of a read-only array property.
                    continue;
                }

                var target = propertyHelper.GetValue(controller);
                if (value == null || target == null)
                {
                    // Nothing to do when source or target is null.
                    continue;
                }

                if (!metadata.IsCollectionType)
                {
                    // Not a collection model.
                    continue;
                }

                // Handle a read-only collection property.
                var propertyAddRange = CallPropertyAddRangeOpenGenericMethod.MakeGenericMethod(
                    metadata.ElementMetadata.ModelType);
                propertyAddRange.Invoke(obj: null, parameters: new[] { target, value });
            }
        }

        private async Task<IDictionary<string, object>> PopulateArgumentsAsync(
            OperationBindingContext operationContext,
            IList<ParameterDescriptor> parameterMetadata)
        {
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal);

            // Perf: Avoid allocations
            for (var i = 0; i < parameterMetadata.Count; i++)
            {
                var parameter = parameterMetadata[i];
                var modelBindingResult = await BindModelAsync(parameter, operationContext);
                if (modelBindingResult != null && modelBindingResult.Value.IsModelSet)
                {
                    arguments[parameter.Name] = modelBindingResult.Value.Model;
                }
            }

            return arguments;
        }

        private OperationBindingContext GetOperationBindingContext(ControllerContext context)
        {
            return new OperationBindingContext
            {
                ActionContext = context,
                InputFormatters = context.InputFormatters,
                ModelBinder = new CompositeModelBinder(context.ModelBinders),
                ValidatorProvider = new CompositeModelValidatorProvider(context.ValidatorProviders),
                MetadataProvider = _modelMetadataProvider,
                ValueProvider = new CompositeValueProvider(context.ValueProviders),
            };
        }
    }
}
