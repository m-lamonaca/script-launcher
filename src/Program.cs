using Cocona;
using Spectre.Console;

var app = CoconaLiteApp.Create();

app.AddCommand(RootCommand);
app.Run();

static async Task RootCommand(
    [Option("extensions", new[] { 'x' }, Description = "Comma separated list of script extensions")] string? extensions,
    [Option("depth", new[] { 'd' }, Description = "Search depth")] int depth = 1,
    [Option("elevated", new[] { 'e' }, Description = "Run with elevated privileges")] bool elevated = false,
    [Option("group", new[] { 'g' }, Description = "Group scripts by folder")] bool group = false,
    [Option("brief", new[] { 'b' }, Description = "Show brief information")] bool brief = false,
    [Argument(Name = "Directory", Description = "Starting directory")] string directory = ".")
{
    if (!Directory.Exists(directory))
    {
        AnsiConsole.Markup($"[red]The directory '{directory}' does not exist.[/]");
        Environment.ExitCode = 1;
        return;
    }

    FileInfo[] files;
    var finder = new ScriptFinder(extensions, directory, depth);

    if (group)
    {
        var dict = finder.GetScriptsByDirectory();

        if (dict.Count == 0)
        {
            AnsiConsole.Markup($"[red]No scripts script files found in '{finder.RootDirectory}' with extensions '{string.Join(", ", finder.Extensions)}'[/]");
            Environment.ExitCode = 1;
            return;
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
        AnsiConsole.Markup($"[red]No scripts script files found in '{finder.RootDirectory}' with extensions '{string.Join(", ", finder.Extensions)}'[/]");
        Environment.ExitCode = 1;
        return;
    }

    var prompt = PromptConstructor.GetScriptPrompt(files, brief);
    var scripts = AnsiConsole.Prompt(prompt);

    await ScriptExecutor.ExecAsync(scripts, elevated);
}
