using System.Text;
using Spectre.Console;

static class PromptConstructor
{
    const int ScriptListSize = 15;

    private static Style SelectionHighlight => new(decoration: Decoration.Bold | Decoration.Underline);

    private static string FileStyle(FileInfo info, bool brief)
    {
        var builder = new StringBuilder();

        if (!brief)
        {
            builder.Append($"[blue]{info.DirectoryName}{Path.DirectorySeparatorChar}[/]");
        }

        builder
            .Append($"[orangered1]{Path.GetFileNameWithoutExtension(info.Name)}[/]")
            .Append($"[greenyellow]{info.Extension}[/]");

        return builder.ToString();
    }

    private static string DirectoryStyle(DirectoryInfo info) => $"[blue]{info}[/]";

    public static MultiSelectionPrompt<FileInfo> GetScriptPrompt(FileInfo[] files, bool brief)
    {
        var prompt = new MultiSelectionPrompt<FileInfo>()
        .Title("Select the scripts to execute:")
        .NotRequired()
        .PageSize(ScriptListSize)
        .InstructionsText("[grey](Press [blue]<space>[/] to toggle a script, [green]<enter>[/] to accept)[/]")
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .UseConverter(x => FileStyle(x, brief))
        .HighlightStyle(SelectionHighlight)
        .AddChoices(files);

        return prompt;
    }

    public static SelectionPrompt<DirectoryInfo> GetDirectoryPrompt(DirectoryInfo[] directories)
    {
        var prompt = new SelectionPrompt<DirectoryInfo>()
        .Title("Select a directory:")
        .PageSize(ScriptListSize)
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .UseConverter(DirectoryStyle)
        .HighlightStyle(SelectionHighlight)
        .AddChoices(directories);

        return prompt;
    }
}
