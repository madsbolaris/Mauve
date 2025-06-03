using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polly;
using Polly.Registry;

namespace Mauve.RPA.Outlook;

public class PageHelpers(ILogger<PageHelpers> logger, IReadOnlyPolicyRegistry<string> policyRegistry)
{
    private readonly ILogger<PageHelpers> _logger = logger;
    private readonly IAsyncPolicy _refreshPolicy = policyRegistry.Get<IAsyncPolicy>("PageRefreshRetry");
    private const string OutlookUrl = "https://outlook.office.com/mail/inbox";

    public async Task RefreshPageAsync(IPage page)
    {
        await _refreshPolicy.ExecuteAsync(async () =>
        {
            _logger.LogInformation("[PageHelpers] Refreshing Outlook Web");

            await page.GotoAsync(OutlookUrl, new() { WaitUntil = WaitUntilState.Load });

            await page.WaitForSelectorAsync("div[role='option'][data-focusable-row='true']");
            await page.AddStyleTagAsync(new()
            {
                Content = "[data-app-section='NotificationPane'] { display: none !important; }"
            });

            _logger.LogInformation("[PageHelpers] Page refresh succeeded");
        });
    }
}
