using System.Net;
using System.Text;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class CloudinaryReservedAssetUploadTests
{
    [Fact]
    public async Task ReservedId_IsSentUnchangedWithoutOverwrite()
    {
        var adId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var assetId =
            $"wayhunter/advertisements/{adId:D}/{operationId:N}";

        var handler = new FakeHandler(async request =>
        {
            var form = Assert.IsType<MultipartFormDataContent>(
                request.Content);

            var fields = new Dictionary<string, string>();

            foreach (var part in form)
            {
                var key = part.Headers.ContentDisposition?
                    .Name?.Trim('"');

                if (key is not null)
                    fields[key] = await part.ReadAsStringAsync();
            }

            Assert.Equal(assetId, fields["public_id"]);
            Assert.Equal("false", fields["overwrite"]);
            Assert.Equal("false", fields["unique_filename"]);

            var url =
                $"https://res.cloudinary.com/testcloud/image/upload/v123/{assetId}.png";

            var json = System.Text.Json.JsonSerializer.Serialize(
                new { public_id = assetId, secure_url = url });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json, Encoding.UTF8, "application/json")
            };
        });

        var storage = new CloudinaryAdvertisementImageStorage(
            new HttpClient(handler), "testcloud", "key", "secret");

        var image = new AdvertisementValidatedImage(
            new byte[] { 1, 2, 3 }, "image/png", ".png");

        var result = await storage.UploadReservedAsync(
            adId, assetId, image);

        Assert.Equal(assetId, result.AssetId);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ForeignAssetId_IsRejectedWithoutNetworkRequest()
    {
        var handler = new FakeHandler(_ =>
            throw new Exception("Network request forbidden."));

        var storage = new CloudinaryAdvertisementImageStorage(
            new HttpClient(handler), "testcloud", "key", "secret");

        var foreignId =
            AdvertisementImageAssetPolicy.CreateAssetId(Guid.NewGuid());

        var image = new AdvertisementValidatedImage(
            new byte[] { 1, 2, 3 }, "image/png", ".png");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.UploadReservedAsync(
                Guid.NewGuid(),
                foreignId,
                image));

        Assert.Equal(0, handler.Calls);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage,
            Task<HttpResponseMessage>> _handler;

        public int Calls { get; private set; }

        public FakeHandler(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return _handler(request);
        }
    }
}