using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class CloudinaryAdvertisementImageStorageTests
{
    private static readonly AdvertisementValidatedImage Image =
        new(new byte[] { 1, 2, 3 }, "image/png", ".png");

    private static CloudinaryAdvertisementImageStorage Create(
        HttpMessageHandler handler)
    {
        return new CloudinaryAdvertisementImageStorage(
            new HttpClient(handler),
            "testcloud",
            "test-key",
            "test-secret");
    }

    [Fact]
    public async Task SuccessfulUpload_UsesOwnedIdAndHttps()
    {
        var advertisementId = Guid.NewGuid();
        string? submittedId = null;

        var handler = new FakeHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);

            Assert.Equal(
                "https://api.cloudinary.com/v1_1/testcloud/image/upload",
                request.RequestUri!.ToString());

            Assert.Equal(
                "Basic",
                request.Headers.Authorization?.Scheme);

            var credentials = Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    request.Headers.Authorization!.Parameter!));

            Assert.Equal("test-key:test-secret", credentials);

            var multipart = Assert.IsType<MultipartFormDataContent>(
                request.Content);

            var fields = new Dictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition?
                    .Name?.Trim('"');

                if (!string.IsNullOrWhiteSpace(name))
                {
                    fields[name] = await part.ReadAsStringAsync();
                }
            }

            Assert.True(fields.ContainsKey("public_id"));
            Assert.True(fields.ContainsKey("overwrite"));
            Assert.True(fields.ContainsKey("unique_filename"));

            Assert.Equal("false", fields["overwrite"]);
            Assert.Equal("false", fields["unique_filename"]);

            submittedId = fields["public_id"];

            Assert.True(
                AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                    advertisementId,
                    submittedId));
            var url =
                $"https://res.cloudinary.com/testcloud/image/upload/v123/{submittedId}.png";

            var json = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    public_id = submittedId,
                    secure_url = url
                });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var storage = Create(handler);

        var result = await storage.UploadAsync(
            advertisementId,
            Image);

        Assert.Equal(submittedId, result.AssetId);
        Assert.StartsWith("https://", result.HttpsUrl);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ForgedCloudinaryUrl_IsRejected()
    {
        var advertisementId = Guid.NewGuid();

        var handler = new FakeHandler(async request =>
        {
            var multipart = Assert.IsType<MultipartFormDataContent>(
                request.Content);

            string? submittedId = null;

            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition?
                    .Name?.Trim('"');

                if (name == "public_id")
                {
                    submittedId = await part.ReadAsStringAsync();
                }
            }

            Assert.NotNull(submittedId);

            Assert.True(
                AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                    advertisementId,
                    submittedId));

            var json = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    public_id = submittedId,
                    secure_url =
                        "https://res.cloudinary.com/testcloud/image/upload/v123/foreign.png"
                });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var storage = Create(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.UploadAsync(advertisementId, Image));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ProviderFailure_DoesNotReturnAsset()
    {
        var handler = new FakeHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable)));

        var storage = Create(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.UploadAsync(Guid.NewGuid(), Image));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ForgedProviderAssetId_IsRejected()
    {
        var handler = new FakeHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"public_id\":\"foreign/asset\",\"secure_url\":\"https://res.cloudinary.com/testcloud/image/upload/v123/image.png\"}",
                        Encoding.UTF8,
                        "application/json")
                }));

        var storage = Create(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.UploadAsync(Guid.NewGuid(), Image));
    }

    [Fact]
    public async Task InvalidAdvertisementId_DoesNotCallProvider()
    {
        var handler = new FakeHandler(_ =>
            throw new Exception("Provider must not be called."));

        var storage = Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.UploadAsync(Guid.Empty, Image));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Delete_RemainsFailClosed()
    {
        var handler = new FakeHandler(_ =>
            throw new Exception("Provider must not be called."));

        var storage = Create(handler);

        var ownedId =
            AdvertisementImageAssetPolicy.CreateAssetId(
                Guid.NewGuid());

        var deleted = await storage.TryDeleteOwnedAssetAsync(
            ownedId);

        Assert.False(deleted);
        Assert.Equal(0, handler.Calls);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage,
            Task<HttpResponseMessage>> _handler;

        public int Calls { get; private set; }

        public FakeHandler(
            Func<HttpRequestMessage,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return await _handler(request);
        }
    }
}