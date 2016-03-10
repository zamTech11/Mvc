// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class HeaderModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context.BindingInfo.BindingSource.CanAcceptDataFrom(BindingSource.Header))
            {
                return new Binder();
            }

            return null;
        }

        private class Binder : IModelBinder
        {
            public Task BindModelAsync(ModelBroContext bindingContext)
            {
                if (bindingContext == null)
                {
                    throw new ArgumentNullException(nameof(bindingContext));
                }

                var request = bindingContext.HttpContext.Request;
                var modelMetadata = bindingContext.Metadata;

                // Property name can be null if the model metadata represents a type (rather than a property or parameter).
                var headerName = bindingContext.FieldName;
                object model = null;
                if (bindingContext.ModelType == typeof(string))
                {
                    string value = request.Headers[headerName];
                    if (value != null)
                    {
                        model = value;
                    }
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(bindingContext.ModelType))
                {
                    var values = request.Headers.GetCommaSeparatedValues(headerName);
                    if (values.Length > 0)
                    {
                        model = values;
                    }
                }

                if (model == null)
                {
                    bindingContext.Result = ModelBindingResult.Failed(bindingContext.ModelName);
                    return TaskCache.CompletedTask;
                }
                else
                {
                    bindingContext.ModelState.SetModelValue(
                        bindingContext.ModelName,
                        request.Headers.GetCommaSeparatedValues(headerName),
                        request.Headers[headerName]);

                    bindingContext.Result = ModelBindingResult.Success(bindingContext.ModelName, model);
                    return TaskCache.CompletedTask;
                }
            }
        }
    }
}
