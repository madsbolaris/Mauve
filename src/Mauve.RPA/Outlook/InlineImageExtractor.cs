using Mauve.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polly;
using Polly.Registry;

public class InlineImageExtractor(
    ILogger<InlineImageExtractor> logger,
    IReadOnlyPolicyRegistry<string> policyRegistry)
{
    private readonly IAsyncPolicy _imageRetryPolicy = policyRegistry.Get<IAsyncPolicy>("ImageRetry");

    public async Task<List<OutlookEmailImage>> ExtractInlineImagesAsync(IPage page, IElementHandle msg)
    {
        var result = new List<OutlookEmailImage>();
        var imgElements = await msg.QuerySelectorAllAsync("img");

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

                result.Add(new OutlookEmailImage(cid, alt ?? string.Empty, new(src), bytes, ext));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to download or process image with CID {Cid} in conversation", cid);
            }
        }

        return result;
    }
}
