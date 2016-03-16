using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Internal;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public class BodyModelBinderFactory : IModelBinderFactory
    {
        private readonly Func<Stream, Encoding, TextReader> _readerFactory;

        public BodyModelBinderFactory(IHttpRequestStreamReaderFactory readerFactory)
        {
            _readerFactory = readerFactory.CreateReader;
        }

        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context.BindingInfo.BindingSource == BindingSource.Body)
            {
                return new Binder(_readerFactory);
            }

            return null;
        }

        private class Binder : IModelBinder
        {
            private readonly Func<Stream, Encoding, TextReader> _readerFactory;

            public Binder(Func<Stream, Encoding, TextReader> readerFactory)
            {
                _readerFactory = readerFactory;
            }

            public async Task BindModelAsync(ModelBindingContext bindingContext)
            {
                if (bindingContext == null)
                {
                    throw new ArgumentNullException(nameof(bindingContext));
                }
                
                var httpContext = bindingContext.HttpContext;
                var key = "$body";

                var formatterContext = new InputFormatterContext(
                    httpContext,
                    _readerFactory,
                    bindingContext.ModelType);

                var formatters = bindingContext.OperationBindingContext.InputFormatters;
                var formatter = formatters.FirstOrDefault(f => f.CanRead(formatterContext));

                if (formatter == null)
                {
                    var message = Resources.FormatUnsupportedContentType(
                        bindingContext.HttpContext.Request.ContentType);

                    var exception = new UnsupportedContentTypeException(message);
                    bindingContext.ModelState.AddModelError(key, exception, bindingContext.Metadata);

                    // This model binder is the only handler for the Body binding source and it cannot run twice. Always
                    // tell the model binding system to skip other model binders and never to fall back i.e. indicate a
                    // fatal error.
                    bindingContext.Result = ModelBindingResult.Failed(key);
                    return;
                }

                try
                {
                    var previousCount = bindingContext.ModelState.ErrorCount;
                    var result = await formatter.ReadAsync(formatterContext);
                    var model = result.Model;

                    if (result.HasError)
                    {
                        // Formatter encountered an error. Do not use the model it returned. As above, tell the model
                        // binding system to skip other model binders and never to fall back.
                        foreach (var error in result.Errors)
                        {
                            // TODO include exception
                            bindingContext.ModelState.TryAddModelError(
                                ModelNames.CreatePropertyModelName(key, error.Key),
                                error.Value.ErrorMessage);
                        }


                        bindingContext.Result = ModelBindingResult.Failed(key);
                        return;
                    }

                    bindingContext.Result = ModelBindingResult.Success(key, model);
                    return;
                }
                catch (Exception ex)
                {
                    bindingContext.ModelState.AddModelError(key, ex, bindingContext.Metadata);

                    // This model binder is the only handler for the Body binding source and it cannot run twice. Always
                    // tell the model binding system to skip other model binders and never to fall back i.e. indicate a
                    // fatal error.
                    bindingContext.Result = ModelBindingResult.Failed(key);
                    return;
                }
            }
        }
    }
}
