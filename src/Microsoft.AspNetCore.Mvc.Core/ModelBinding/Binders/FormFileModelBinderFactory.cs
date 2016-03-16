// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    /// <summary>
    /// <see cref="IModelBinder"/> implementation to bind posted files to <see cref="IFormFile"/>.
    /// </summary>
    public class FormFileModelBinderFactory : IModelBinderFactory
    {
        public IModelBinder Create(ModelBroFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var modelType = context.ModelType;
            if (modelType == typeof(IFormFile) || 
                typeof(IEnumerable<IFormFile>).IsAssignableFrom(modelType))
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

            /// <inheritdoc />
            public Task BindModelAsync(ModelBindingContext bindingContext)
            {
                if (bindingContext == null)
                {
                    throw new ArgumentNullException(nameof(bindingContext));
                }

                // This method is optimized to use cached tasks when possible and avoid allocating
                // using Task.FromResult or async state machines.
                var modelType = _metadata.Get<ITypeMetadata>().ModelType;
                var createFileCollection = modelType == typeof(IFormFileCollection) &&
                    !bindingContext.Metadata.IsReadOnly;
                if (!createFileCollection && !ModelBindingHelper.CanGetCompatibleCollection<IFormFile>(bindingContext))
                {
                    // Silently fail and stop other model binders running if unable to create an instance or use the
                    // current instance.
                    bindingContext.Result = ModelBindingResult.Failed(_metadata.ModelName);
                    return TaskCache.CompletedTask;
                }

                ICollection<IFormFile> postedFiles;
                if (createFileCollection)
                {
                    postedFiles = new List<IFormFile>();
                }
                else
                {
                    postedFiles = ModelBindingHelper.GetCompatibleCollection<IFormFile>(bindingContext);
                }

                return BindModelCoreAsync(bindingContext, postedFiles);
            }

            private async Task BindModelCoreAsync(ModelBindingContext bindingContext, ICollection<IFormFile> postedFiles)
            {
                Debug.Assert(postedFiles != null);

                var modelName = _metadata.ModelName;
                var modelType = _metadata.Get<ITypeMetadata>().ModelType;

                await GetFormFilesAsync(modelName, bindingContext, postedFiles);

                object value;
                if (modelType == typeof(IFormFile))
                {
                    if (postedFiles.Count == 0)
                    {
                        // Silently fail if the named file does not exist in the request.
                        bindingContext.Result = ModelBindingResult.Failed(modelName);
                        return;
                    }

                    value = postedFiles.First();
                }
                else
                {
                    if (postedFiles.Count == 0 && !bindingContext.IsTopLevelObject)
                    {
                        // Silently fail if no files match. Will bind to an empty collection (treat empty as a success
                        // case and not reach here) if binding to a top-level object.
                        bindingContext.Result = ModelBindingResult.Failed(modelName);
                        return;
                    }

                    // Perform any final type mangling needed.
                    if (modelType == typeof(IFormFile[]))
                    {
                        Debug.Assert(postedFiles is List<IFormFile>);
                        value = ((List<IFormFile>)postedFiles).ToArray();
                    }
                    else if (modelType == typeof(IFormFileCollection))
                    {
                        Debug.Assert(postedFiles is List<IFormFile>);
                        value = new FileCollection((List<IFormFile>)postedFiles);
                    }
                    else
                    {
                        value = postedFiles;
                    }
                }

                bindingContext.ValidationState.Add(value, new ValidationStateEntry()
                {
                    Key = modelName,
                    SuppressValidation = true
                });

                bindingContext.ModelState.SetModelValue(
                    modelName,
                    rawValue: null,
                    attemptedValue: null);

                bindingContext.Result = ModelBindingResult.Success(modelName, value);
            }

            private async Task GetFormFilesAsync(
                string modelName,
                ModelBindingContext bindingContext,
                ICollection<IFormFile> postedFiles)
            {
                var request = bindingContext.OperationBindingContext.HttpContext.Request;
                if (request.HasFormContentType)
                {
                    var form = await request.ReadFormAsync();

                    foreach (var file in form.Files)
                    {
                        // If there is an <input type="file" ... /> in the form and is left blank.
                        if (file.Length == 0 && string.IsNullOrEmpty(file.FileName))
                        {
                            continue;
                        }

                        if (file.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                        {
                            postedFiles.Add(file);
                        }
                    }
                }
            }
        }

        private class FileCollection : ReadOnlyCollection<IFormFile>, IFormFileCollection
        {
            public FileCollection(List<IFormFile> list)
                : base(list)
            {
            }

            public IFormFile this[string name] => GetFile(name);

            public IFormFile GetFile(string name)
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    var file = Items[i];
                    if (string.Equals(name, file.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }

                return null;
            }

            public IReadOnlyList<IFormFile> GetFiles(string name)
            {
                var files = new List<IFormFile>();
                for (var i = 0; i < Items.Count; i++)
                {
                    var file = Items[i];
                    if (string.Equals(name, file.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(file);
                    }
                }

                return files;
            }
        }
    }
}