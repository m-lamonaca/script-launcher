using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScriptLauncher;

public sealed class RootCommand : AsyncCommand<RootCommandSettings>
{
    private const int Failure = 1;
    private const int Success = 0;

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        RootCommandSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Directory.Exists(settings.Directory))
        {
            AnsiConsole.Markup($"[red]The directory '{settings.Directory}' does not exist.[/]");
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

        try
        {
            await ScriptExecutor.ExecAsync(scripts, settings.Elevated).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException
            )
        {
            AnsiConsole.Markup($"[red]{ex.Message}[/]");
            return Failure;
        }

        return Success;
    }
}

public sealed class RootCommandSettings : CommandSettings
{
    [Description("List of script extensions to search for")]
    [CommandOption("-x|--extensions <EXTENSIONS>")]
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
    public string[] Extensions { get; init; } = [".ps1", ".*sh", ".bat", ".cmd", ".nu"];

    [Description("Search depth")]
    [CommandOption("-d|--depth")]
    [DefaultValue(3)]
    public int Depth { get; init; }

    [Description("Run with elevated privileges")]
    [CommandOption("-e|--elevated")]
    public bool Elevated { get; init; }

    [Description("Group scripts by folder")]
    [CommandOption("-g|--group")]
    public bool Group { get; init; }

    [Description("Show brief information")]
    [CommandOption("-b|--brief")]
    public bool Brief { get; init; }

    [Description("Starting directory (Default: .)")]
    [CommandArgument(0, "[path]")]
    [DefaultValue(".")]
    public string Directory { get; init; } = string.Empty;
}
