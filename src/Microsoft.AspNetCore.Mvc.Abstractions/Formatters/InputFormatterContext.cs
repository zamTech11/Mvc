// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Microsoft.AspNetCore.Mvc.Formatters
{
    /// <summary>
    /// A context object used by an input formatter for deserializing the request body into an object.
    /// </summary>
    public class InputFormatterContext
    {
        /// <summary>
        /// Creates a new instance of <see cref="InputFormatterContext"/>.
        /// </summary>
        /// <param name="httpContext">
        /// The <see cref="Http.HttpContext"/> for the current operation.
        /// </param>
        /// <param name="readerFactory">
        /// A delegate which can create a <see cref="TextReader"/> for the request body.
        /// </param>
        /// <param name="modelType">
        /// The <see cref="Type"/> of the model object to deserialize.
        /// </param>
        public InputFormatterContext(
            HttpContext httpContext,
            Func<Stream, Encoding, TextReader> readerFactory,
            Type modelType)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (readerFactory == null)
            {
                throw new ArgumentNullException(nameof(readerFactory));
            }

            HttpContext = httpContext;
            ReaderFactory = readerFactory;
            ModelType = modelType;
        }

        /// <summary>
        /// Gets the <see cref="Http.HttpContext"/> associated with the current operation.
        /// </summary>
        public HttpContext HttpContext { get; }

        /// <summary>
        /// Gets the requested <see cref="System.Type"/> of the request body deserialization.
        /// </summary>
        public Type ModelType { get; }

        /// <summary>
        /// Gets a delegate which can create a <see cref="TextReader"/> for the request body.
        /// </summary>
        public Func<Stream, Encoding, TextReader> ReaderFactory { get; }
    }
}
