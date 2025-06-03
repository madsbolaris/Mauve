using System.Diagnostics;
using System.Threading.Channels;

namespace Mauve.RPA;

public class MessageMonitorService
{
    private string _dbPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Library/Messages/chat.db";
    private long _lastRowId = -1;

    public async Task MonitorMessagesAsync(Func<string, Task> onNewMessage, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = GetLatestMessage();
                if (message != null && message.Value.RowId != _lastRowId)
                {
                    _lastRowId = message.Value.RowId;
                    await onNewMessage(message.Value.Text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor Error] {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private (long RowId, string Text)? GetLatestMessage()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sqlite3",
            Arguments = $"\"{_dbPath}\" \"SELECT rowid, text FROM message ORDER BY date DESC LIMIT 1;\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        var output = process!.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (string.IsNullOrWhiteSpace(output))
            return null;

        var parts = output.Trim().Split('|');
        if (parts.Length < 2) return null;

        return (long.Parse(parts[0]), parts[1]);
    }
}
