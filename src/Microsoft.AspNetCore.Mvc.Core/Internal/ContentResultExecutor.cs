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

        private const string DefaultContentType = "text/plain; charset=utf-8";
        private readonly ArrayPool<byte> _byteArrayPool;
        private readonly ArrayPool<char> _charArrayPool;
        private readonly ILogger<ContentResultExecutor> _logger;

        public ContentResultExecutor(ILogger<ContentResultExecutor> logger, ArrayPool<byte> byteArrayPool, ArrayPool<char> charArrayPool)
        {
            _logger = logger;
            _byteArrayPool = byteArrayPool;
            _charArrayPool = charArrayPool;
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
                var charBuffer = _charArrayPool.Rent(MaxCharacterChunkSize);
                byte[] byteBuffer = null;

                try
                {
                    // Since the buffer returned by ArrayPool could be greater than the size requested for, try to utilize
                    // that size instead
                    var requiredByteBufferLength = resolvedContentTypeEncoding.GetMaxByteCount(charBuffer.Length);
                    byteBuffer = _byteArrayPool.Rent(requiredByteBufferLength);

                    var encoder = resolvedContentTypeEncoding.GetEncoder();

                    var sourceIndex = 0;
                    var flushEncoder = false;
                    while (sourceIndex < result.Content.Length)
                    {
                        var numOfCharsToCopy = Math.Min((result.Content.Length - sourceIndex), charBuffer.Length);

                        result.Content.CopyTo(
                            sourceIndex,
                            charBuffer,
                            0,
                            numOfCharsToCopy);

                        sourceIndex += numOfCharsToCopy;

                        if (sourceIndex >= result.Content.Length)
                        {
                            flushEncoder = false;
                        }

                        var bytesWritten = encoder.GetBytes(charBuffer, 0, numOfCharsToCopy, byteBuffer, 0, flushEncoder);

                        await response.Body.WriteAsync(byteBuffer, 0, bytesWritten);
                    }
                }
                finally
                {
                    // free the buffers
                    ArrayPool<char>.Shared.Return(charBuffer);

                    if (byteBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(byteBuffer);
                    }
                }
            }
        }
    }
}
