// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Mvc
{
    public class ContentResult : ActionResult
    {
        private readonly string DefaultContentType = new MediaTypeHeaderValue("text/plain")
        {
            Encoding = Encoding.UTF8
        }.ToString();

        private const int BufferSize = 1024;

        /// <summary>
        /// Gets or set the content representing the body of the response.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the Content-Type header for the response.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int? StatusCode { get; set; }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ContentResult>();

            var response = context.HttpContext.Response;

            string resolvedContentType;
            Encoding resolvedContentTypeEncoding;
            ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
                ContentType,
                response.ContentType,
                DefaultContentType,
                out resolvedContentType,
                out resolvedContentTypeEncoding);

            response.ContentType = resolvedContentType;

            if (StatusCode != null)
            {
                response.StatusCode = StatusCode.Value;
            }

            logger.ContentResultExecuting(resolvedContentType);

            if (Content != null)
            {
                response.ContentLength = resolvedContentTypeEncoding.GetByteCount(Content);

                var requiredLength = resolvedContentTypeEncoding.GetMaxByteCount(BufferSize);
                var byteBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);

                try
                {
                    var sourceIndex = 0;

                    while (sourceIndex < Content.Length)
                    {
                        var charCount = Math.Min(Content.Length - sourceIndex, BufferSize);

                        var bytesWritten = resolvedContentTypeEncoding.GetBytes(Content, sourceIndex, charCount, byteBuffer, 0);

                        await response.Body.WriteAsync(byteBuffer, 0, bytesWritten);

                        sourceIndex += charCount;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(byteBuffer);
                }
            }
        }
    }
}
