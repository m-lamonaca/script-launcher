using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<RootCommand>();
app.Configure(x => x.SetApplicationName("script-launcher"));
return app.Run(args);

sealed class RootCommand : AsyncCommand<RootCommandSettings>
{
    private const int Failure = 1;
    private const int Success = 0;

    public override async Task<int> ExecuteAsync(
        [NotNull] CommandContext context,
        [NotNull] RootCommandSettings settings
    )
    {
        if (!Directory.Exists(settings.Directory))
        {
            AnsiConsole.Markup($"[red]The directory '{settings.Directory}' does not exist.[/]");
            // Environment.ExitCode = 1;
            return Failure;
        }

        FileInfo[] files;
        var finder = new ScriptFinder(settings.Extensions, settings.Directory, settings.Depth);

        if (settings.Group)
        {
            var dict = finder.GetScriptsByDirectory();

            if (dict.Count == 0)
            {
                AnsiConsole.Markup(
                    $"[red]No scripts script files found in '{finder.RootDirectory}' with extensions '{string.Join(", ", finder.Extensions)}'[/]"
                );
                return Failure;
            }

            var dirPrompt = PromptConstructor.GetDirectoryPrompt(dict.Keys.ToArray());
            var directoryInfo = AnsiConsole.Prompt(dirPrompt);
            files = dict[directoryInfo];
        }
        else
        {
            files = finder.GetScripts();
        }

        if (files.Length == 0)
        {
            AnsiConsole.Markup(
                $"[red]No scripts script files found in '{finder.RootDirectory}' with extensions '{string.Join(", ", finder.Extensions)}'[/]"
            );
            return Failure;
        }

        var prompt = PromptConstructor.GetScriptPrompt(files, settings.Brief);
        var scripts = AnsiConsole.Prompt(prompt);

        await ScriptExecutor.ExecAsync(scripts, settings.Elevated);

        return Success;
    }
}

internal class RootCommandSettings : CommandSettings
{
    [Description("Comma separated list of script extensions")]
    [CommandOption("-x|--extensions")]
    public string? Extensions { get; init; }

    [Description("Search depth")]
    [CommandOption("-d|--depth")]
    public int Depth { get; init; } = 1;

    [Description("Run with elevated privileges")]
    [CommandOption("-e|--elevated")]
    public bool Elevated { get; init; } = false;

    [Description("Group scripts by folder")]
    [CommandOption("-g|--group")]
    public bool Group { get; init; } = false;

    [Description("Show brief information")]
    [CommandOption("-b|--brief")]
    public bool Brief { get; init; } = false;

    [Description("Starting directory (Default: .)")]
    [CommandArgument(0, "<path>")]
    public string Directory { get; init; } = ".";
}
