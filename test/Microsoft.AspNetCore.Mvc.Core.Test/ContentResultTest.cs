// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.TestCommon;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Net.Http.Headers;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc
{
    public class ContentResultTest
    {
        [Fact]
        public async Task ContentResult_Response_NullContent_SetsContentTypeAndEncoding()
        {
            // Arrange
            var contentResult = new ContentResult
            {
                Content = null,
                ContentType = new MediaTypeHeaderValue("text/plain")
                {
                    Encoding = Encoding.UTF7
                }.ToString()
            };
            var httpContext = GetHttpContext();
            var actionContext = GetActionContext(httpContext);

            // Act
            await contentResult.ExecuteResultAsync(actionContext);

            // Assert
            MediaTypeAssert.Equal("text/plain; charset=utf-7", httpContext.Response.ContentType);
        }

        public static TheoryData<MediaTypeHeaderValue, string, string, string, byte[]> ContentResultContentTypeData
        {
            get
            {
                // contentType, content, responseContentType, expectedContentType, expectedData
                return new TheoryData<MediaTypeHeaderValue, string, string, string, byte[]>
                {
                    {
                        null,
                        "κόσμε",
                        null,
                        "text/plain; charset=utf-8",
                        new byte[] { 206, 186, 225, 189, 185, 207, 131, 206, 188, 206, 181 } //utf-8 without BOM
                    },
                    {
                        new MediaTypeHeaderValue("text/foo"),
                        "κόσμε",
                        null,
                        "text/foo",
                        new byte[] { 206, 186, 225, 189, 185, 207, 131, 206, 188, 206, 181 } //utf-8 without BOM
                    },
                    {
                        MediaTypeHeaderValue.Parse("text/foo;p1=p1-value"),
                        "κόσμε",
                        null,
                        "text/foo; p1=p1-value",
                        new byte[] { 206, 186, 225, 189, 185, 207, 131, 206, 188, 206, 181 } //utf-8 without BOM
                    },
                    {
                        new MediaTypeHeaderValue("text/foo") { Encoding = Encoding.ASCII },
                        "abcd",
                        null,
                        "text/foo; charset=us-ascii",
                        new byte[] { 97, 98, 99, 100 }
                    },
                    {
                        null,
                        "abcd",
                        "text/bar",
                        "text/bar",
                        new byte[] { 97, 98, 99, 100 }
                    },
                    {
                        null,
                        "abcd",
                        "application/xml; charset=us-ascii",
                        "application/xml; charset=us-ascii",
                        new byte[] { 97, 98, 99, 100 }
                    },
                    {
                        null,
                        "abcd",
                        "Invalid content type",
                        "Invalid content type",
                        new byte[] { 97, 98, 99, 100 }
                    },
                    {
                        new MediaTypeHeaderValue("text/foo") { Charset = "us-ascii" },
                        "abcd",
                        "text/bar",
                        "text/foo; charset=us-ascii",
                        new byte[] { 97, 98, 99, 100 }
                    },
                };
            }
        }

        [Theory]
        [MemberData(nameof(ContentResultContentTypeData))]
        public async Task ContentResult_ExecuteResultAsync_SetContentTypeAndEncoding_OnResponse(
            MediaTypeHeaderValue contentType,
            string content,
            string responseContentType,
            string expectedContentType,
            byte[] expectedContentData)
        {
            // Arrange
            var contentResult = new ContentResult
            {
                Content = content,
                ContentType = contentType?.ToString()
            };
            var httpContext = GetHttpContext();
            var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;
            httpContext.Response.ContentType = responseContentType;
            var actionContext = GetActionContext(httpContext);

            // Act
            await contentResult.ExecuteResultAsync(actionContext);

            // Assert
            var finalResponseContentType = httpContext.Response.ContentType;
            Assert.Equal(expectedContentType, finalResponseContentType);
            Assert.Equal(expectedContentData, memoryStream.ToArray());
            Assert.Equal(expectedContentData.Length, httpContext.Response.ContentLength);
        }

        public static TheoryData<string, string> ContentResult_WritesDataCorrectly_ForDifferentContentSizesData
        {
            get
            {
                var maxCharacterChunkSize = ContentResultExecutor.MaxCharacterChunkSize;

                // content, contentType
                return new TheoryData<string, string>
                {
                    {  string.Empty, "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize - 1), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize + 1), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize - 2), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize + 2), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize - 3), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize + 3), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize * 2), "text/plain; charset=utf-8" },
                    {  new string('a', maxCharacterChunkSize * 3), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 2) - 1), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 2) - 2), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 2) - 3), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 2) + 1), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 2) + 2), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 2) + 3), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 3) - 1), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 3) - 2), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 3) - 3), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 3) + 1), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 3) + 2), "text/plain; charset=utf-8" },
                    {  new string('a', (maxCharacterChunkSize * 3) + 3), "text/plain; charset=utf-8" },

                    {  new string('色', maxCharacterChunkSize), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize - 1), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize + 1), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize - 2), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize + 2), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize - 3), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize + 3), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize * 2), "text/plain; charset=utf-16" },
                    {  new string('色', maxCharacterChunkSize * 3), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 2) - 1), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 2) - 2), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 2) - 3), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 2) + 1), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 2) + 2), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 2) + 3), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 3) - 1), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 3) - 2), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 3) - 3), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 3) + 1), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 3) + 2), "text/plain; charset=utf-16" },
                    {  new string('色', (maxCharacterChunkSize * 3) + 3), "text/plain; charset=utf-16" },

                    {  new string('色', maxCharacterChunkSize), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize - 1), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize + 1), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize - 2), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize + 2), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize - 3), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize + 3), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize * 2), "text/plain; charset=utf-32" },
                    {  new string('色', maxCharacterChunkSize * 3), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 2) - 1), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 2) - 2), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 2) - 3), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 2) + 1), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 2) + 2), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 2) + 3), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 3) - 1), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 3) - 2), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 3) - 3), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 3) + 1), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 3) + 2), "text/plain; charset=utf-32" },
                    {  new string('色', (maxCharacterChunkSize * 3) + 3), "text/plain; charset=utf-32" },
                };
            }
        }

        [Theory]
        [MemberData(nameof(ContentResult_WritesDataCorrectly_ForDifferentContentSizesData))]
        public async Task ContentResult_WritesDataCorrectly_ForDifferentContentSizes(string content, string contentType)
        {
            // Arrange
            var contentResult = new ContentResult
            {
                Content = content,
                ContentType = contentType
            };
            var httpContext = GetHttpContext();
            var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;
            var actionContext = GetActionContext(httpContext);
            var encoding = MediaTypeHeaderValue.Parse(contentType).Encoding;

            // Act
            await contentResult.ExecuteResultAsync(actionContext);

            // Assert
            memoryStream.Seek(0, SeekOrigin.Begin);
            var streamReader = new StreamReader(memoryStream, encoding);
            var actualContent = await streamReader.ReadToEndAsync();
            Assert.Equal(content, actualContent);
        }

        [Theory]
        [InlineData(ContentResultExecutor.MaxCharacterChunkSize)]
        [InlineData(ContentResultExecutor.MaxCharacterChunkSize * 2)]
        [InlineData(ContentResultExecutor.MaxCharacterChunkSize * 3)]
        public async Task ContentResult_WritesDataCorrectly_ForCharactersHavingSurrogatePairs(int characterSize)
        {
            // Arrange
            // Here "𐐀" (called Deseret Long I) actually represents 2 characters. Try to make this character split across
            // the boundary
            var content = new string('a', characterSize - 1) + "𐐀";
            var contentType = "text/plain; charset=utf-8";
            var contentResult = new ContentResult
            {
                Content = content,
                ContentType = contentType
            };
            var httpContext = GetHttpContext();
            var memoryStream = new MemoryStream();
            httpContext.Response.Body = memoryStream;
            var actionContext = GetActionContext(httpContext);
            var encoding = MediaTypeHeaderValue.Parse(contentType).Encoding;

            // Act
            await contentResult.ExecuteResultAsync(actionContext);

            // Assert
            memoryStream.Seek(0, SeekOrigin.Begin);
            var streamReader = new StreamReader(memoryStream, encoding);
            var actualContent = await streamReader.ReadToEndAsync();
            Assert.Equal(content, actualContent);
        }

        private static ActionContext GetActionContext(HttpContext httpContext)
        {
            var routeData = new RouteData();
            routeData.Routers.Add(Mock.Of<IRouter>());

            return new ActionContext(httpContext,
                                    routeData,
                                    new ActionDescriptor());
        }

        private static IServiceCollection CreateServices(params ViewComponentDescriptor[] descriptors)
        {
            // An array pool could return a buffer which is greater or equal to the size of the default character
            // chunk size. Since the tests here depend on a specifc character buffer size to test boundary conditions,
            // make sure to only return a buffer of that size.
            var charArrayPool = new Mock<ArrayPool<char>>();
            charArrayPool
                .Setup(ap => ap.Rent(ContentResultExecutor.MaxCharacterChunkSize))
                .Returns(new char[ContentResultExecutor.MaxCharacterChunkSize]);

            var services = new ServiceCollection();
            services.AddSingleton(new ContentResultExecutor(
                new Logger<ContentResultExecutor>(NullLoggerFactory.Instance),
                ArrayPool<byte>.Shared,
                charArrayPool.Object));
            return services;
        }

        private static HttpContext GetHttpContext()
        {
            var services = CreateServices();

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = services.BuildServiceProvider();

            return httpContext;
        }
    }
}