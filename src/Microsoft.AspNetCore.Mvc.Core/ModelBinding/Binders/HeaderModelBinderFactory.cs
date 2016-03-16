// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class HeaderModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context.BindingInfo.BindingSource.CanAcceptDataFrom(BindingSource.Header))
            {
                return new Binder(context.Metadata);
            }

            return null;
        }

        private class Binder : IModelBinder
        {
            private readonly ModelBroMetadata _metadata;

            public Binder(ModelBroMetadata metadata)
            {
                if (metadata == null)
                {
                    throw new ArgumentNullException(nameof(metadata));
                }

                _metadata = metadata;
            }

            public Task BindModelAsync(ModelBindingContext bindingContext)
            {
                if (bindingContext == null)
                {
                    throw new ArgumentNullException(nameof(bindingContext));
                }

                var request = bindingContext.HttpContext.Request;

                var headerName = _metadata.ModelName;
                var modelType = _metadata.Get<ITypeMetadata>().ModelType;
                object model = null;
                if (modelType == typeof(string))
                {
                    string value = request.Headers[headerName];
                    if (value != null)
                    {
                        model = value;
                    }
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(modelType))
                {
                    var values = request.Headers.GetCommaSeparatedValues(headerName);
                    if (values.Length > 0)
                    {
                        model = values;
                    }
                }

                if (model == null)
                {
                    bindingContext.Result = ModelBindingResult.Failed(_metadata.ModelName);
                    return TaskCache.CompletedTask;
                }
                else
                {
                    bindingContext.ModelState.SetModelValue(
                        _metadata.ModelName,
                        request.Headers.GetCommaSeparatedValues(headerName),
                        request.Headers[headerName]);

                    bindingContext.Result = ModelBindingResult.Success(_metadata.ModelName, model);
                    return TaskCache.CompletedTask;
                }
            }
        }
    }
}
