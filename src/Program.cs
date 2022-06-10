using System.ComponentModel;
using System.Diagnostics;
using System.Text;
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
        .Title("Select a script the scripts to execute:")
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

static class ScriptExecutor
{
    public static async Task ExecAsync(List<FileInfo> files, bool elevated)
    {
        await Parallel.ForEachAsync(files, (x, ct) => ExecAsync(x, elevated, ct));
    }

    public static async ValueTask ExecAsync(FileInfo file, bool elevated, CancellationToken cancellationToken = default)
    {
        var process = GetExecutableProcessInfo(file, elevated);

        if (process is null) return;

        try
        {
            await (Process.Start(process)?.WaitForExitAsync(cancellationToken) ?? Task.CompletedTask);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            AnsiConsole.Markup($"[red]{ex.Message}[/]");
        }
    }

    private static ProcessStartInfo? GetExecutableProcessInfo(FileInfo file, bool elevated)
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
    static readonly string[] DefaultExtensions = new[] { ".ps1", ".*sh", ".bat", ".cmd" };
    public string[] Extensions { get; }
    public string RootDirectory { get; }
    public int Depth { get; }
    private readonly EnumerationOptions _options;

    public ScriptFinder(string? extensions, string directory, int depth)
    {
        Extensions = extensions?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet()
        .Select(x => $".{x.TrimStart('.')}")
        .ToArray() ?? DefaultExtensions;

        Depth = depth;
        RootDirectory = directory;

        _options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = Depth > 0,
            MaxRecursionDepth = Depth,
        };
    }

    private IEnumerable<FileInfo> GetScriptFilesWithExtension(string extension)
    {
        try
        {
            var filenames = Directory.GetFiles(RootDirectory, $"*{extension}", _options);
            return filenames.Select(x => new FileInfo(x));
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<FileInfo>();
        }
    }

    public FileInfo[] GetScripts() =>
        Extensions.Select(GetScriptFilesWithExtension).SelectMany(x => x).ToArray();

    public IDictionary<DirectoryInfo, FileInfo[]> GetScriptsByDirectory() =>
        Extensions
        .Select(GetScriptFilesWithExtension)
        .SelectMany(x => x)
        .GroupBy(x => x.DirectoryName!)
        .OrderBy(x => x.Key)
        .ToDictionary(x => new DirectoryInfo(x.Key), x => x.ToArray());
}
