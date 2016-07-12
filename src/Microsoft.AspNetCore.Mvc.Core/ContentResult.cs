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

        private const int DefaultCharBufferSize = 4 * 1024;
        private const int DefaultByteBufferSize = 8 * 1024;

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

                // Calculate the byte length to set the content length header
                var charBuffer = ArrayPool<char>.Shared.Rent(DefaultCharBufferSize);
                int sourceIndex = 0;
                long byteCount = 0;
                var encoder = resolvedContentTypeEncoding.GetEncoder();

                while (sourceIndex < Content.Length)
                {
                    var countOfCharsToCopy = Math.Min((Content.Length - sourceIndex), charBuffer.Length);

                    Content.CopyTo(sourceIndex, charBuffer, 0, countOfCharsToCopy);

                    byteCount += encoder.GetByteCount(charBuffer, 0, countOfCharsToCopy, flush: false);

                    sourceIndex += countOfCharsToCopy;
                }

                response.ContentLength = byteCount;

                // write the content to stream
                var byteBuffer = ArrayPool<byte>.Shared.Rent(DefaultByteBufferSize);

                try
                {
                    sourceIndex = 0;
                    while (sourceIndex < Content.Length)
                    {
                        var numOfCharsToCopy = Math.Min((Content.Length - sourceIndex), charBuffer.Length);

                        Content.CopyTo(
                            sourceIndex,
                            charBuffer,
                            0,
                            numOfCharsToCopy);

                        sourceIndex += numOfCharsToCopy;

                        var bytesWritten = encoder.GetBytes(charBuffer, 0, numOfCharsToCopy, byteBuffer, 0, flush: false);

                        await response.Body.WriteAsync(byteBuffer, 0, bytesWritten);
                    }
                }
                finally
                {
                    // free the buffers
                    ArrayPool<char>.Shared.Return(charBuffer);
                    ArrayPool<byte>.Shared.Return(byteBuffer);
                }
            }
            else
            {
                response.ContentLength = 0;
            }
        }
    }
}
