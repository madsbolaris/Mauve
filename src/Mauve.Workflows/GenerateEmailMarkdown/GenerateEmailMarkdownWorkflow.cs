using Mauve.Workflows.GenerateEmailMarkdown.Steps;
using Microsoft.SemanticKernel;

namespace Mauve.Workflows.GenerateEmailMarkdown;

public class GenerateEmailMarkdownWorkflow()
{
    public KernelProcess Create()
    {
        var process = new ProcessBuilder("GenerateEmailMarkdown");

        var htmlToMarkdown = process.AddStepFromType<HtmlToMarkdownConverter>();
        var saveEmail = process.AddStepFromType<SaveMarkdownEmail>();
        var deleteEmail = process.AddStepFromType<DeleteRawEmail>();

        process
            .OnInputEvent(GenerateEmailMarkdownEvents.NewEmail)
            .SendEventTo(new ProcessFunctionTargetBuilder(htmlToMarkdown));

        htmlToMarkdown
            .OnFunctionResult()
            .SendEventTo(new ProcessFunctionTargetBuilder(saveEmail));

        saveEmail
            .OnEvent(GenerateEmailMarkdownEvents.EmailSaved)
            .SendEventTo(new ProcessFunctionTargetBuilder(deleteEmail));

        return process.Build();
    }
}
