using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Api.Services.HomeAdsAnalytics;

/// <summary>
/// Cloudinary upload transport for validated M7 Advertisement images.
/// Not registered for production use until explicitly configured.
/// </summary>
public sealed class CloudinaryAdvertisementImageStorage
    : IAdvertisementImageStorage, IAdvertisementImageReservedStorage
{
    private readonly HttpClient _httpClient;
    private readonly string _cloudName;
    private readonly string _apiKey;
    private readonly string _apiSecret;

    public CloudinaryAdvertisementImageStorage(
        HttpClient httpClient,
        string cloudName,
        string apiKey,
        string apiSecret)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));

        if (string.IsNullOrWhiteSpace(cloudName) ||
            !Regex.IsMatch(
                cloudName,
                @"\A[a-zA-Z0-9_-]+\z",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Invalid Cloudinary cloud name.",
                nameof(cloudName));
        }

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new ArgumentException(
                "Cloudinary backend credentials are required.");
        }

        _cloudName = cloudName;
        _apiKey = apiKey;
        _apiSecret = apiSecret;
    }

    public async Task<AdvertisementStoredImage> UploadAsync(
        Guid advertisementId,
        AdvertisementValidatedImage image,
        CancellationToken cancellationToken = default)
    {
        if (advertisementId == Guid.Empty)
            throw new ArgumentException(
                "Invalid Advertisement ID.",
                nameof(advertisementId));

        ArgumentNullException.ThrowIfNull(image);

        if (image.Content is null || image.Content.Length == 0)
            throw new ArgumentException(
                "Validated image is empty.",
                nameof(image));

        if (image.ContentType is not ("image/jpeg" or "image/png") ||
            image.Content.Length > AdvertisementImageValidator.MaxBytes)
        {
            throw new ArgumentException(
                "Unsupported Advertisement image.",
                nameof(image));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await UploadReservedAsync(
            advertisementId,
            AdvertisementImageAssetPolicy.CreateAssetId(advertisementId),
            image,
            cancellationToken);
    }

    public async Task<AdvertisementStoredImage> UploadReservedAsync(
        Guid advertisementId,
        string assetId,
        AdvertisementValidatedImage image,
        CancellationToken cancellationToken = default)
    {
        if (!AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                advertisementId, assetId))
        {
            throw new ArgumentException(
                "Asset ID does not belong to this advertisement.",
                nameof(assetId));
        }

        ArgumentNullException.ThrowIfNull(image);

        if (image.Content is null ||
            image.Content.Length == 0 ||
            image.Content.Length > AdvertisementImageValidator.MaxBytes ||
            image.ContentType is not ("image/jpeg" or "image/png"))
        {
            throw new ArgumentException(
                "Invalid advertisement image.",
                nameof(image));
        }

        cancellationToken.ThrowIfCancellationRequested();


        var endpoint = new Uri(
            $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_apiKey}:{_apiSecret}"));

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        using var form = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(image.Content);
        imageContent.Headers.ContentType =
            new MediaTypeHeaderValue(image.ContentType);

        form.Add(imageContent, "file", "advertisement");
        form.Add(new StringContent(assetId), "public_id");
        form.Add(new StringContent("false"), "overwrite");
        form.Add(new StringContent("false"), "unique_filename");

        request.Content = form;

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Do not expose provider responses or credentials.
            throw new InvalidOperationException(
                "Advertisement image upload provider failed.");
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var root = document.RootElement;

        if (!root.TryGetProperty("public_id", out var idProperty) ||
            !root.TryGetProperty("secure_url", out var urlProperty) ||
            idProperty.ValueKind != JsonValueKind.String ||
            urlProperty.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "Cloudinary returned an invalid upload result.");
        }

        var returnedId = idProperty.GetString();
        var returnedUrl = urlProperty.GetString();

        if (!string.Equals(
                assetId,
                returnedId,
                StringComparison.Ordinal) ||
            !AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                advertisementId,
                returnedId) ||
            !Uri.TryCreate(
                returnedUrl,
                UriKind.Absolute,
                out var secureUri) ||
            secureUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                secureUri.Host,
                "res.cloudinary.com",
                StringComparison.OrdinalIgnoreCase) ||
            !secureUri.IsDefaultPort ||
            !string.IsNullOrEmpty(secureUri.UserInfo) ||
            !string.IsNullOrEmpty(secureUri.Query) ||
            !string.IsNullOrEmpty(secureUri.Fragment) ||
            !Regex.IsMatch(
                secureUri.AbsolutePath,
                $@"\A/{Regex.Escape(_cloudName)}/image/upload/v[0-9]+/{Regex.Escape(returnedId!)}\.(?:png|jpg|jpeg)\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Cloudinary upload ownership verification failed.");
        }

        return new AdvertisementStoredImage(
            returnedId!,
            returnedUrl!);
    }

    public Task<bool> TryDeleteOwnedAssetAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Fail closed until authoritative SQL reference checks
        // and auditable reconciliation are integrated.
        return Task.FromResult(false);
    }
}