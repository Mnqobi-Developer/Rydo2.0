using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Rydo.Infrastructure.Drivers;

public sealed class DriverDocumentStorageOptions
{
    public const string SectionName = "DriverDocumentStorage";

    public string Provider { get; init; } = "Local";

    public string LocalRoot { get; init; } = "data/driver-documents";

    public string SupabaseUrl { get; init; } = string.Empty;

    public string SupabaseServiceRoleKey { get; init; } = string.Empty;

    public string SupabaseBucket { get; init; } = "driver-documents";
}

public interface IDriverDocumentObjectStorage
{
    Task UploadAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class DriverDocumentStorageHttpClient
{
    public HttpClient Client { get; } = new()
    {
        Timeout = TimeSpan.FromSeconds(90),
    };
}

public sealed class LocalDriverDocumentObjectStorage(
    IOptions<DriverDocumentStorageOptions> options) : IDriverDocumentObjectStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalRoot);

    public async Task UploadAsync(
        string objectKey,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var destination = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await content.CopyToAsync(destination, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        Stream stream = new FileStream(
            ResolvePath(objectKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string objectKey)
    {
        var path = Path.GetFullPath(Path.Combine(
            _root,
            objectKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document object key escaped the configured storage root.");
        }
        return path;
    }
}

public sealed class SupabaseDriverDocumentObjectStorage(
    IOptions<DriverDocumentStorageOptions> options,
    DriverDocumentStorageHttpClient httpClient) : IDriverDocumentObjectStorage
{
    private readonly DriverDocumentStorageOptions _options = options.Value;

    public async Task UploadAsync(
        string objectKey,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, UploadUrl(objectKey));
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await httpClient.Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Supabase Storage upload failed with HTTP {(int)response.StatusCode}.");
        }
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, AuthenticatedUrl(objectKey));
        var response = await httpClient.Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new FileNotFoundException("The protected driver document could not be read.");
        }
        return new ResponseOwnedStream(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            response);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, UploadUrl(objectKey));
        using var response = await httpClient.Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.SupabaseServiceRoleKey);
        request.Headers.TryAddWithoutValidation("apikey", _options.SupabaseServiceRoleKey);
        return request;
    }

    private string UploadUrl(string objectKey) =>
        $"{_options.SupabaseUrl.TrimEnd('/')}/storage/v1/object/{Encode(_options.SupabaseBucket)}/{EncodePath(objectKey)}";

    private string AuthenticatedUrl(string objectKey) =>
        $"{_options.SupabaseUrl.TrimEnd('/')}/storage/v1/object/authenticated/{Encode(_options.SupabaseBucket)}/{EncodePath(objectKey)}";

    private static string EncodePath(string value) => string.Join('/', value.Split('/').Select(Encode));

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private sealed class ResponseOwnedStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            response.Dispose();
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
