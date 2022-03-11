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

    var files = finder.GetScriptFiles();

    if (files.Length == 0)
    {
        AnsiConsole.Markup($"[red]No scripts script files found in '{directory}' with extensions '{string.Join("|", fileExtensions)}'[/]");
        Environment.ExitCode = ErrorExitCode;
        return;
    }

    if (multiple)
    {
        var prompt = new MultiSelectionPrompt<FileInfo>()
        .Title("Select a script the scripts to execute:")
        .NotRequired()
        .PageSize(ScriptListSize)
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .InstructionsText("[grey](Press [blue]<space>[/] to toggle a script, [green]<enter>[/] to accept)[/]")
        .UseConverter(PromptDecorator.GetStyledOption)
        .HighlightStyle(PromptDecorator.SelectionHighlight)
        .AddChoices(files);

        var scripts = AnsiConsole.Prompt(prompt);

        scripts.ForEach(x => ScriptExecutor.Exec(x, elevated));
    }
    else
    {
        var prompt = new SelectionPrompt<FileInfo>()
        .Title("Select a script to execute:")
        .PageSize(ScriptListSize)
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .UseConverter(PromptDecorator.GetStyledOption)
        .HighlightStyle(PromptDecorator.SelectionHighlight)
        .AddChoices(files);

        var script = AnsiConsole.Prompt(prompt);

        ScriptExecutor.Exec(script, elevated);
    }
};

static class PromptDecorator
{
    internal static string GetStyledOption(FileInfo info)
    {
        var directory = $"[blue]{info.DirectoryName ?? "."}{Path.DirectorySeparatorChar}[/]";
        var filename = $"[orangered1]{Path.GetFileNameWithoutExtension(info.Name)}[/]";
        var extension = $"[greenyellow]{Path.GetExtension(info.Name)}[/]";

        return $"{directory}{filename}{extension}";
    }

    internal static Style SelectionHighlight => new Style(decoration: Decoration.Bold | Decoration.Underline);
}

static class ScriptExecutor
{
    internal static void Exec(FileInfo file, bool elevated)
    {
        var process = GetExecutableProcess(file, elevated);

        if (process is null) return;

        // todo: handle exceptions (shell not found)
        Process.Start(process)?.WaitForExit();
    }

    private static ProcessStartInfo? GetExecutableProcess(FileInfo file, bool elevated)
    {
        return file.Extension switch
        {
            ".bat" or ".cmd" => new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/Q /C .\\{file.Name}",
                Verb = elevated ? "runas /user:Administrator" : string.Empty,
                WorkingDirectory = file.DirectoryName
            },
            ".ps1" => new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File .\\{file.Name}",
                Verb = elevated ? "runas /user:Administrator" : string.Empty,
                WorkingDirectory = file.DirectoryName
            },
            ".sh" => new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c ./{file.Name}",
                Verb = elevated ? "sudo" : string.Empty,
                WorkingDirectory = file.DirectoryName
            },
            ".zsh" => new ProcessStartInfo
            {
                FileName = "zsh",
                Arguments = $"-c ./{file.Name}",
                Verb = elevated ? "sudo" : string.Empty,
                WorkingDirectory = file.DirectoryName
            },
            ".fish" => new ProcessStartInfo
            {
                FileName = "fish",
                Arguments = $"-c ./{file.Name}",
                Verb = elevated ? "sudo" : string.Empty,
                WorkingDirectory = file.DirectoryName
            },
            _ => null
        };
    }
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

    internal readonly IEnumerable<FileInfo> GetScriptFiles(string extension)
    {
        try
        {
            var filenames = Directory.GetFiles(RootFolder, $"*.{extension}", _options);
            return filenames.Select(x => new FileInfo(x));
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<FileInfo>();
        }
    }

    internal readonly FileInfo[] GetScriptFiles() =>
        Extensions.Select(GetScriptFiles).SelectMany(x => x).ToArray();
}