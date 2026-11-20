using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Api.Services.HomeAdsAnalytics;

public static class AdvertisementMultipartImageValidator
{
    public static async Task<AdvertisementValidatedImage> ValidateAsync(
        IFormCollection form,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);

        // Exactly one file, named "file".
        // No additional form fields.
        if (form.Files.Count != 1 || form.Count != 0)
        {
            throw new ArgumentException(
                "Exactly one image file is required.");
        }

        var file = form.Files[0];

        if (!string.Equals(
                file.Name,
                "file",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Invalid image form field.");
        }

        if (file.Length <= 0 ||
            file.Length > AdvertisementImageValidator.MaxBytes)
        {
            throw new ArgumentException(
                "Image file size exceeds allowed limits.");
        }

        // Never trust the client-supplied filename.
        // Read only a bounded amount of file content.
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();

        var buffer = new byte[64 * 1024];
        var total = 0;

        while (true)
        {
            var read = await input.ReadAsync(
                buffer.AsMemory(),
                cancellationToken);

            if (read == 0)
                break;

            total = checked(total + read);

            if (total > AdvertisementImageValidator.MaxBytes)
            {
                throw new ArgumentException(
                    "Image file size exceeds allowed limits.");
            }

            output.Write(buffer, 0, read);
        }

        if (total == 0 || total != file.Length)
        {
            throw new ArgumentException(
                "Image file content length is invalid.");
        }

        return AdvertisementImageValidator.Validate(
            output.ToArray(),
            file.ContentType);
    }
}
