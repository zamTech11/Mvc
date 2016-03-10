using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class ServicesModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context.BindingInfo.BindingSource == BindingSource.Services)
            {
                return new Binder(context.ModelType);
            }

            return null;
        }

        private class Binder : IModelBinder
        {
            private readonly Type _type;

            public Binder(Type type)
            {
                _type = type;
            }

            public Task BindModelAsync(ModelBroContext context)
            {
                context.Result = context.RequestServices.GetRequiredService(_type);
                return TaskCache.CompletedTask;
            }
        }
    }
}
