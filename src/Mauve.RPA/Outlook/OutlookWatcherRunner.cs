using Microsoft.Extensions.Hosting;

namespace Mauve.RPA.Outlook;

public class OutlookWatcherRunner : IHostedService
{
    private readonly OutlookWatcher _watcher;
    private Task? _watcherTask;

    public OutlookWatcherRunner(OutlookWatcher watcher)
    {
        _watcher = watcher;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcherTask = Task.Run(() => _watcher.StartAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask; // Don't block
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Optionally add logic to stop _watcher if it supports cancellation
        if (_watcherTask is not null)
        {
            await _watcherTask;
        }
    }
}
