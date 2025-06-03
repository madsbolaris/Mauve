namespace Mauve.Core;

public interface IRPAWatcher
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
