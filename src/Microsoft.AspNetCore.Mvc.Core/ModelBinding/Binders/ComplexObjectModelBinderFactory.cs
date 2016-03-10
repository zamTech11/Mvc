using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class ComplexObjectModelBroFactory : IModelBinderFactory
    {
        private Stack<KeyValuePair<ModelBroMetadata, IModelBinder>> _stack = new Stack<KeyValuePair<ModelBroMetadata, IModelBinder>>();

        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (!context.Metadata.Get<ITypeMetadata>().IsComplexType)
            {
                return null;
            }
            
            foreach (var item in _stack)
            {
                if (item.Key.Equals(context.Metadata))
                {
                    return item.Value;
                }
            }

            var binder = new Binder();
            _stack.Push(new KeyValuePair<ModelBroMetadata, IModelBinder>(context.Metadata, binder));

            foreach (var child in context.Metadata.Children)
            {
                binder.PropertyBinders.Add(child, context.CreateBro(child));
            }

            _stack.Pop();

            return binder;
        }

        private class Binder : IModelBinder
        {
            public IDictionary<ModelBroMetadata, IModelBinder> PropertyBinders { get; } = new Dictionary<ModelBroMetadata, IModelBinder>();

            public Task BindModelAsync(ModelBroContext bindingContext)
            {
                throw new NotImplementedException();
            }
        }
    }
}
