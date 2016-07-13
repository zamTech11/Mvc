// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class ContentResultExecutor
    {
        /// <summary>
        /// The maximum number of characters that are encoded and written to stream in a single pass.
        /// </summary>
        public const int MaxCharacterChunkSize = 1024;

        private readonly string DefaultContentType = new MediaTypeHeaderValue("text/plain")
        {
            Encoding = Encoding.UTF8
        }.ToString();

        private readonly ILogger<ContentResultExecutor> _logger;

        public ContentResultExecutor(ILogger<ContentResultExecutor> logger)
        {
            _logger = logger;
        }

        public async Task ExecuteAsync(ActionContext context, ContentResult result)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var response = context.HttpContext.Response;

            string resolvedContentType;
            Encoding resolvedContentTypeEncoding;
            ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
                result.ContentType,
                response.ContentType,
                DefaultContentType,
                out resolvedContentType,
                out resolvedContentTypeEncoding);

            response.ContentType = resolvedContentType;

            if (result.StatusCode != null)
            {
                response.StatusCode = result.StatusCode.Value;
            }

            _logger.ContentResultExecuting(resolvedContentType);

            if (result.Content != null)
            {
                response.ContentLength = resolvedContentTypeEncoding.GetByteCount(result.Content);

                var requiredLength = resolvedContentTypeEncoding.GetMaxByteCount(MaxCharacterChunkSize);
                var byteBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);

                try
                {
                    var sourceIndex = 0;

                    while (sourceIndex < result.Content.Length)
                    {
                        var charCount = Math.Min(result.Content.Length - sourceIndex, MaxCharacterChunkSize);

                        var bytesWritten = resolvedContentTypeEncoding.GetBytes(result.Content, sourceIndex, charCount, byteBuffer, 0);

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
