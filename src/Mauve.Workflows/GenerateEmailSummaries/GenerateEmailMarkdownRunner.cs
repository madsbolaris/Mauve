// using System.Text.Json;
// using Mauve.Core.Helpers;
// using Mauve.Core.Models;
// using Mauve.Core.Settings;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
// using Microsoft.SemanticKernel;

// namespace Mauve.Workflows.GenerateEmailSummaries;

// public class GenerateEmailSummariesRunner(
//     GenerateEmailSummariesWorkflow workflow,
//     IOptions<PathSettings> pathOptions,
//     ILogger<GenerateEmailSummariesRunner> logger,
//     IServiceProvider serviceProvider,
//     JsonSerializerOptions jsonOptions
// ) : IHostedService
// {
//     private readonly GenerateEmailSummariesWorkflow _workflow = workflow;
//     private readonly string _watchDir = PathUtils.ExpandHome(pathOptions.Value.NewEmailStorageDirectory);
//     private readonly ILogger<GenerateEmailSummariesRunner> _logger = logger;
//     private readonly CancellationTokenSource _cts = new();
//     private Task? _runnerTask;
//     private readonly Kernel _kernel = new(serviceProvider);

//     public Task StartAsync(CancellationToken cancellationToken)
//     {
//         _runnerTask = Task.Run(() => WatchLoopAsync(_cts.Token));
//         _logger.LogInformation("GenerateEmailSummariesRunner started. Watching directory: {Directory}", _watchDir);
//         return Task.CompletedTask;
//     }

//     public Task StopAsync(CancellationToken cancellationToken)
//     {
// #pragma warning disable VSTHRD103 // Call async methods when in an async method
//         _cts.Cancel();
// #pragma warning restore VSTHRD103 // Call async methods when in an async method
//         _logger.LogInformation("GenerateEmailSummariesRunner stopping...");
//         return _runnerTask ?? Task.CompletedTask;
//     }

//     private async Task WatchLoopAsync(CancellationToken cancellationToken)
//     {
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             try
//             {
//                 var jsonFiles = Directory.GetFiles(_watchDir, "*.json", SearchOption.AllDirectories)
//                                         .ToArray();

//                 await Parallel.ForEachAsync(jsonFiles, cancellationToken, async (file, ct) =>
//                 {
//                     OutlookMessage? message = null;

//                     try
//                     {
//                         var json = await File.ReadAllTextAsync(file, ct);
//                         message = JsonSerializer.Deserialize<OutlookMessage>(json, jsonOptions);
//                     }
//                     catch (Exception ex)
//                     {
//                         _logger.LogWarning(ex, "Failed to read or deserialize email file: {File}", file);
//                         return;
//                     }

//                     if (message is null) return;

//                     try
//                     {
//                         var process = _workflow.Create();
//                         await process.StartAsync(_kernel, new KernelProcessEvent
//                         {
//                             Id = GenerateEmailSummariesEvents.NewEmail,
//                             Data = message
//                         });

//                         _logger.LogInformation("Generated markdown for message {MessageId}", message.MessageId);
//                     }
//                     catch (Exception ex)
//                     {
//                         _logger.LogError(ex, "Markdown generation failed for message {MessageId}", message.MessageId);
//                     }
//                 });

//                 TryDeleteEmptyDirectories(_watchDir);
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Unexpected error during directory watch loop");
//             }

//             await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
//         }
//     }

//     private void TryDeleteEmptyDirectories(string root)
//     {
//         foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
//         {
//             try
//             {
//                 if (!Directory.EnumerateFileSystemEntries(directory).Any())
//                 {
//                     Directory.Delete(directory);
//                     _logger.LogDebug("Deleted empty directory: {Directory}", directory);
//                 }
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogDebug(ex, "Failed to delete directory: {Directory}", directory);
//             }
//         }
//     }

// }
