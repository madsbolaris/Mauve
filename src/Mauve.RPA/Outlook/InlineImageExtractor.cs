using Mauve.Core.Helpers;
using Mauve.Core.Models;
using Mauve.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Polly;
using Polly.Registry;

public class InlineImageExtractor(
    IOptions<PathSettings> options,
    ILogger<InlineImageExtractor> logger,
    IReadOnlyPolicyRegistry<string> policyRegistry)
{
    private readonly string _baseDir = PathUtils.ExpandHome(options.Value.NewEmailStorageDirectory);
    private readonly IAsyncPolicy _imageRetryPolicy = policyRegistry.Get<IAsyncPolicy>("ImageRetry");

    public async Task<List<OutlookEmailImage>> ExtractInlineImagesAsync(
        IPage page,
        IElementHandle msg,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var result = new List<OutlookEmailImage>();
        var imgElements = await msg.QuerySelectorAllAsync("img");

        var folder = Path.Combine(_baseDir, conversationId);

        // Ensure base folder exists
        Directory.CreateDirectory(folder);

        // Ensure image directory exists
        var imageDir = Path.Combine(folder, "cid");
        Directory.CreateDirectory(imageDir);

        foreach (var img in imgElements)
        {
            var alt = await img.GetAttributeAsync("alt");
            var originalSrc = await img.GetAttributeAsync("originalsrc");
            if (originalSrc is null || !originalSrc.StartsWith("cid:")) continue;

            var cid = originalSrc[4..];
            var src = await img.GetAttributeAsync("src");
            if (string.IsNullOrEmpty(src) || src.StartsWith("data:")) continue;

            try
            {
                var response = await _imageRetryPolicy.ExecuteAsync(() => page.Context.APIRequest.GetAsync(src));

                if (!response.Ok)
                {
                    logger.LogWarning("Image fetch failed for CID {Cid} with status {StatusCode}", cid, response.Status);
                    continue;
                }

                var bytes = await response.BodyAsync();
                var contentType = response.Headers["content-type"].ToString();

                var ext = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    "image/svg+xml" => ".svg",
                    _ => ".bin"
                };

                var filename = $"{cid}{ext}";
                var fullPath = Path.Combine(imageDir, filename);
                await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
                logger.LogDebug("Saved image to {Path}", fullPath);

                result.Add(new OutlookEmailImage($"cid/{filename}", alt ?? string.Empty));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to download or save image with CID {Cid}", cid);
            }
        }

        return result;
    }
}
