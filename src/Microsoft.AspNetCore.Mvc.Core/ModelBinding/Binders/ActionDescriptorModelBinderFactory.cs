// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class ActionDescriptorModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            var actionDescriptorMetadata = context.Metadata as ActionDescriptorBroMetadata;
            if (actionDescriptorMetadata == null)
            {
                return null;
            }

            var children = new List<IModelBinder>();

            foreach (var child in actionDescriptorMetadata.Children)
            {
                children.Add(context.CreateBro(child));
            }

            return new Binder(children);
        }

        private class Binder : IModelBinder
        {
            private readonly List<IModelBinder> _children;

            public Binder(List<IModelBinder> children)
            {
                _children = children;
            }

            public async Task BindModelAsync(ModelBindingContext bindingContext)
            {
                foreach (var child in _children)
                {
                    await child.BindModelAsync(bindingContext);
                }
            }
        }
    }
}
