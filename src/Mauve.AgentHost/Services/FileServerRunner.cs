using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Mauve.Core.Helpers;
using Mauve.Core.Settings;

namespace Mauve.AgentHost.Services
{
    public class FileServerRunner(
        IOptions<PathSettings> pathOptions
    ) : IHostedService
    {
        private IHost? _webHost;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateBuilder();

            var emailsWithMarkdownPath = PathUtils.ExpandHome(pathOptions.Value.EmailsWithMarkdownDirectory);

            builder.Services.AddRouting();

            var app = builder.Build();

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(emailsWithMarkdownPath),
                RequestPath = "/files"
            });

            app.MapGet("/", () => Results.Redirect("/files"));

            _webHost = app;
            return app.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _webHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;
        }
    }
}
