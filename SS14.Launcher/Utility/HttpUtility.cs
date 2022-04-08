using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

public static class HttpUtility
{
    private static readonly StringWithQualityHeaderValue ZStdHeader = new StringWithQualityHeaderValue("zstd", 1);

    public static async Task<HttpResponseMessage> SendZStdAsync(
        this HttpClient client,
        HttpRequestMessage message,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
        CancellationToken cancel = default)
    {
        message.Headers.AcceptEncoding.Add(ZStdHeader);

        var response = await client.SendAsync(message, completionOption, cancel);

        if (response.Content.Headers.ContentEncoding.LastOrDefault() == "zstd")
        {
            response.Content = new ZStdHttpContent(response.Content);
        }

        return response;
    }

    // Taken from https://github.com/dotnet/runtime/blob/f89fbb96cabe95db5869e3d44c6b48c1c0f8fc1a/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/DecompressionHandler.cs
    // The original code is Copyright © .NET Foundation and Contributors. All rights reserved. Licensed under the MIT License (MIT).
    public abstract class DecompressedContent : HttpContent
    {
        private readonly HttpContent _originalContent;
        private bool _contentConsumed;

        public DecompressedContent(HttpContent originalContent)
        {
            _originalContent = originalContent;
            _contentConsumed = false;

            // Copy original response headers, but with the following changes:
            //   Content-Length is removed, since it no longer applies to the decompressed content
            //   The last Content-Encoding is removed, since we are processing that here.
            foreach (var (h, v) in originalContent.Headers)
            {
                Headers.Add(h, v);
            }

            Headers.ContentLength = null;
            Headers.ContentEncoding.Clear();
            string? prevEncoding = null;
            foreach (string encoding in originalContent.Headers.ContentEncoding)
            {
                if (prevEncoding != null)
                {
                    Headers.ContentEncoding.Add(prevEncoding);
                }

                prevEncoding = encoding;
            }
        }

        protected abstract Stream GetDecompressedStream(Stream originalStream);

        protected override void SerializeToStream(Stream stream, TransportContext? context,
            CancellationToken cancellationToken)
        {
            using Stream decompressedStream = CreateContentReadStream(cancellationToken);
            decompressedStream.CopyTo(stream);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context,
            CancellationToken cancellationToken)
        {
            using Stream decompressedStream = await CreateContentReadStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            await decompressedStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
        {
            ValueTask<Stream> task = CreateContentReadStreamAsyncCore(async: false, cancellationToken);
            Debug.Assert(task.IsCompleted);
            return task.GetAwaiter().GetResult();
        }

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            CreateContentReadStreamAsyncCore(async: true, cancellationToken).AsTask();

        private async ValueTask<Stream> CreateContentReadStreamAsyncCore(bool async,
            CancellationToken cancellationToken)
        {
            if (_contentConsumed)
            {
                throw new InvalidOperationException("Stream already read");
            }

            _contentConsumed = true;

            Stream originalStream;
            if (async)
            {
                originalStream = await _originalContent.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                originalStream = _originalContent.ReadAsStream(cancellationToken);
            }

            return GetDecompressedStream(originalStream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _originalContent.Dispose();
            }

            base.Dispose(disposing);
        }
    }


    public sealed class ZStdHttpContent : DecompressedContent
    {
        public ZStdHttpContent(HttpContent originalContent) : base(originalContent)
        {
        }

        protected override Stream GetDecompressedStream(Stream originalStream)
        {
            return new ZStdDecompressStream(originalStream);
        }
    }
}
