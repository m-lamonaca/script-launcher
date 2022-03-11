using System.Diagnostics;
using Cocona;
using Spectre.Console;

const int ScriptListSize = 15;
const int ErrorExitCode = 1;

var app = CoconaLiteApp.Create();

app.AddCommand(RootCommand);
app.Run();

static void RootCommand(
    [Option("extensions", new char[] { 'e' }, Description = "Comma separated list of script extensions to search")] string extensions = "*",
    [Option("depth", new char[] { 'd' }, Description = "Folder depth of the search")] int depth = 0,
    [Option("elevated", new char[] { 'E' }, Description = "Run the script with elevated privileges")] bool elevated = false,
    [Option("multiple", new char[] { 'm' }, Description = "Execute multiple scripts")] bool multiple = false,
    [Argument(Name = "Directory", Description = "Directory from which search the scripts")] string directory = ".")
{
    if (!Directory.Exists(directory))
    {
        AnsiConsole.Markup($"[red]The directory '{directory}' does not exist.[/]");
        Environment.ExitCode = ErrorExitCode;
        return;
    }

    var fileExtensions = extensions.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet()
        .ToArray();

    var finder = new ScriptFinder
    {
        Extensions = fileExtensions,
        RootFolder = directory,
        Depth = depth
    };

    var files = finder.GetScriptFiles(fileExtensions);

    if (files.Length == 0)
    {
        AnsiConsole.Markup($"[red]No scripts script files found in '{directory}' with extensions '{string.Join("|", fileExtensions)}'[/]");
        Environment.ExitCode = ErrorExitCode;
        return;
    }

    if (multiple)
    {
        var prompt = new MultiSelectionPrompt<FileInfo>()
        .Title("Select scripts")
        .NotRequired()
        .PageSize(ScriptListSize)
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .InstructionsText("[grey](Press [blue]<space>[/] to toggle a script, [green]<enter>[/] to accept)[/]")
        .AddChoices(files);

        var scripts = AnsiConsole.Prompt(prompt);

        scripts.ForEach(ScriptExecutor.Exec);
    }
    else
    {
        var prompt = new SelectionPrompt<FileInfo>()
        .Title("Select a script")
        .PageSize(ScriptListSize)
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .AddChoices(files);

        var script = AnsiConsole.Prompt(prompt);

        ScriptExecutor.Exec(script);
    }
};

static class ScriptExecutor
{
    internal static void Exec(FileInfo file)
    {
        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-File {file.FullName}",
            UseShellExecute = false,
        };

        Exec(info);
    }

    internal static void Exec(ProcessStartInfo info) => Process.Start(info)?.WaitForExit();
}

readonly struct ScriptFinder
{
    public string RootFolder { get; init; } = ".";
    public string[] Extensions { get; init; } = new[] { "ps1", "*sh", "bat", "cmd" };
    public int Depth { get; init; } = 0;

    private readonly EnumerationOptions _options;

    public ScriptFinder()
    {
        _options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = Depth > 0,
            MaxRecursionDepth = Depth,
        };
    }

    internal readonly FileInfo[] GetScriptFiles(string extension)
    {
        try
        {
            var filenames = Directory.GetFiles(RootFolder, $"*.{extension}", _options);
            return filenames.Select(x => new FileInfo(x)).ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileInfo>();
        }
    }

    internal readonly FileInfo[] GetScriptFiles() => 
        Extensions.Select(GetScriptFiles).SelectMany(x => x).ToArray();
}