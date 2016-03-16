// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Internal;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    /// <summary>
    /// <see cref="IModelBinder"/> implementation to bind models of type <see cref="CancellationToken"/>.
    /// </summary>
    public class CancellationTokenModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.ModelType == typeof(CancellationToken))
            {
                return new Binder();
            }

            return null;
        }

        private class Binder : IModelBinder
        {
            /// <inheritdoc />
            public Task BindModelAsync(ModelBindingContext bindingContext)
            {
                if (bindingContext == null)
                {
                    throw new ArgumentNullException(nameof(bindingContext));
                }

                // We need to force boxing now, so we can insert the same reference to the boxed CancellationToken
                // in both the ValidationState and ModelBindingResult.
                //
                // DO NOT simplify this code by removing the cast.
                var model = (object)bindingContext.HttpContext.RequestAborted;
                bindingContext.ValidationState.Add(model, new ValidationStateEntry() { SuppressValidation = true });
                bindingContext.Result = ModelBindingResult.Success(bindingContext.ModelName, model);

                return TaskCache.CompletedTask;
            }
        }
    }
}
