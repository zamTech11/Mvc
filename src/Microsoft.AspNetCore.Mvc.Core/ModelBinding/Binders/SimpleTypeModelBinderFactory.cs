using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class SimpleTypeModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (!context.Metadata.Get<ITypeMetadata>().IsComplexType)
            {
                return new Binder();
            }

            return null;
        }

        private class Binder : IModelBinder
        {
            public Task BindModelAsync(ModelBindingContext bindingContext)
            {
                if (bindingContext == null)
                {
                    throw new ArgumentNullException(nameof(bindingContext));
                }

                var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
                if (valueProviderResult == ValueProviderResult.None)
                {
                    // no entry
                    return TaskCache.CompletedTask;
                }

                bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

                try
                {
                    var model = valueProviderResult.ConvertTo(bindingContext.ModelType);

                    if (bindingContext.ModelType == typeof(string))
                    {
                        var modelAsString = model as string;
                        if (bindingContext.Metadata.Get<IDisplayMetadata>()?.ConvertEmptyStringToNull == true &&
                            string.IsNullOrEmpty(modelAsString))
                        {
                            model = null;
                        }
                    }

                    // When converting newModel a null value may indicate a failed conversion for an otherwise required
                    // model (can't set a ValueType to null). This detects if a null model value is acceptable given the
                    // current bindingContext. If not, an error is logged.
                    if (model == null && bindingContext.Metadata.Get<ITypeMetadata>().IsReferenceOrNullableType)
                    {
                        var messageProvider = bindingContext.Metadata.Get<IModelBindingMessageProvider>();
                        bindingContext.ModelState.TryAddModelError(
                            bindingContext.ModelName,
                            messageProvider.ValueMustNotBeNullAccessor(
                                valueProviderResult.ToString()));

                        bindingContext.Result = ModelBindingResult.Failed(bindingContext.ModelName);
                        return TaskCache.CompletedTask;
                    }
                    else
                    {
                        bindingContext.Result = ModelBindingResult.Success(bindingContext.ModelName, model);
                        return TaskCache.CompletedTask;
                    }
                }
                catch (Exception exception)
                {
                    bindingContext.ModelState.TryAddModelError(
                        bindingContext.ModelName,
                        exception,
                        bindingContext.Metadata);

                    // Were able to find a converter for the type but conversion failed.
                    // Tell the model binding system to skip other model binders.
                    bindingContext.Result = ModelBindingResult.Failed(bindingContext.ModelName);
                    return TaskCache.CompletedTask;
                }
            }
        }
    }
}