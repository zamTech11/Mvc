// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class BinderTypeModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context.BindingInfo.BinderType != null)
            {
                // TODO: Type Activate
                return (IModelBinder)Activator.CreateInstance(context.BindingInfo.BinderType);
            }

            return null;
        }
    }
}
