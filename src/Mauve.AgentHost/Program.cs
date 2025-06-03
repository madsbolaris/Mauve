using Mauve.RPA.Outlook;
using Polly;
using Polly.Registry;
using Microsoft.Playwright;
using Mauve.Core.Settings;
using Mauve.Workflows.GenerateEmailMarkdown;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mauve.AgentHost.Services;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var root = Path.GetFullPath(Path.Combine(context.HostingEnvironment.ContentRootPath, "../../"));
        config.AddJsonFile(Path.Combine(root, "appsettings.json"), optional: false, reloadOnChange: true);
        config.AddCommandLine(args); 
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;


        // Bind config settings
        services.Configure<PathSettings>(config.GetSection("Paths"));
        services.Configure<OpenAISettings>(config.GetSection("OpenAI"));
        services.Configure<FileServerSettings>(config.GetSection("FileServer"));

        var enabledRunners = config.GetSection("EnabledRunners").Get<List<string>>() ?? new();

        services.AddSingleton(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        });

        services.AddSingleton<IReadOnlyPolicyRegistry<string>>(provider =>
        {
            var registry = new PolicyRegistry
            {
                { "PlaywrightRetry", Policy.Handle<PlaywrightException>(ex => ex.Message.Contains("not attached to the DOM")).WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(200)) },
                { "ImageRetry", Policy.Handle<Exception>().WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(500)) },
                { "FileRetry", Policy.Handle<IOException>().Or<UnauthorizedAccessException>().WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(200)) },
                { "PageRefreshRetry", Policy.Handle<PlaywrightException>().Or<TimeoutException>().WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(500)) },
                { "KernelRetry", Policy.Handle<Exception>().WaitAndRetryAsync(4, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))) }
            };
            return registry;
        });


        // Kernel + AI
        services.AddSingleton<IChatCompletionService>(provider => new OpenAIChatCompletionService(
            modelId: "gpt-4.1-mini",
            apiKey: config["OpenAI:ApiKey"]!
        ));
        services.AddSingleton(provider => new Kernel(provider));

        // Shared services
        services.AddSingleton<MessagePersister>();
        services.AddSingleton<PageHelpers>();
        services.AddSingleton<EmailMover>();
        services.AddSingleton<MessageExtractor>();
        services.AddSingleton<OutlookWatcher>();
        services.AddSingleton<PersonExtractor>();
        services.AddSingleton<InlineImageExtractor>();
        services.AddSingleton<GenerateEmailMarkdownWorkflow>();

        // Conditionally register hosted services
        if (enabledRunners.Contains(nameof(OutlookWatcherRunner), StringComparer.OrdinalIgnoreCase))
        {
            services.AddHostedService<OutlookWatcherRunner>();
        }

        if (enabledRunners.Contains(nameof(GenerateEmailMarkdownRunner), StringComparer.OrdinalIgnoreCase))
        {
            services.AddHostedService<GenerateEmailMarkdownRunner>();
        }
        
        if (enabledRunners.Contains(nameof(FileServerRunner), StringComparer.OrdinalIgnoreCase))
        {
            services.AddHostedService<FileServerRunner>();
        }

    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();