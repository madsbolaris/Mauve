using Mauve.AgentHost.Workflows.NewEmail.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Mauve.AgentHost.Workflows.NewEmail;

public class NewEmailWorkflow()
{
    public static KernelProcess Create()
    {
        var process = new ProcessBuilder("NewEmailWorkflow");

        // Define all steps in the process
        var htmlToMarkdown = process.AddStepFromType<HtmlToMarkdownConverter>();
        var orchestrator = process.AddStepFromType<NewEmailOrchestrator>();
        var saveEmail = process.AddStepFromType<EmailSaver>();
        var summarizeEmail = process.AddStepFromType<EmailSummarizer>();

        // Define input events and initial routing
        process
            .OnInputEvent(Events.NewEmail)
            .SendEventTo(new ProcessFunctionTargetBuilder(htmlToMarkdown));

        htmlToMarkdown
            .OnFunctionResult()
            .SendEventTo(new ProcessFunctionTargetBuilder(orchestrator, "new_email"));

        htmlToMarkdown
            .OnEvent(Events.SaveEmail)
            .SendEventTo(new ProcessFunctionTargetBuilder(saveEmail));

        // orchestrator
        //     .OnEvent(Events.RequestSummary)
        //     .SendEventTo(new ProcessFunctionTargetBuilder(summarizeEmail));

        // summarizeEmail
        //     .OnEvent(Events.SaveEmail)
        //     .SendEventTo(new ProcessFunctionTargetBuilder(saveEmail));

        // summarizeEmail
        //     .OnFunctionResult()
        //     .SendEventTo(new ProcessFunctionTargetBuilder(orchestrator, "new_summary"));

        return process.Build();
    }
}
