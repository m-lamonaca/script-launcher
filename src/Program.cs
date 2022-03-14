﻿using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Cocona;
using Spectre.Console;

const int ScriptListSize = 15;
const int ErrorExitCode = 1;
string[] DefaultExtensions = new[] { "ps1", "*sh", "bat", "cmd" };

var app = CoconaLiteApp.Create();

app.AddCommand(RootCommand);
app.Run();

static async Task RootCommand(
    [Option("extensions", new char[] { 'e' }, Description = "Comma separated list of script extensions to search")] string? extensions,
    [Option("depth", new char[] { 'd' }, Description = "Folder depth of the search")] int depth = 1,
    [Option("elevated", new char[] { 'E' }, Description = "Run the script with elevated privileges")] bool elevated = false,
    [Option("multiple", new char[] { 'm' }, Description = "Execute multiple scripts in parallel")] bool multiple = false,
    [Option("grouped", new char[] { 'g' }, Description = "Group selection bay containing folder")] bool grouped = false,
    [Argument(Name = "Directory", Description = "Directory from which search the scripts")] string directory = ".")
{
    if (!Directory.Exists(directory))
    {
        AnsiConsole.Markup($"[red]The directory '{directory}' does not exist.[/]");
        Environment.ExitCode = ErrorExitCode;
        return;
    }

    var files = Array.Empty<FileInfo>();
    var finder = new ScriptFinder(extensions, directory, depth);

    if (grouped)
    {
        var dict = finder.GetScriptsByDirectory();
        var prompt = new SelectionPrompt<DirectoryInfo>()
            .Title("Select a directory:")
            .PageSize(ScriptListSize)
            .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
            .UseConverter(x => $"[blue]{x}[/]")
            .HighlightStyle(PromptDecorator.SelectionHighlight)
            .AddChoices(dict.Keys);

        var directoryInfo = AnsiConsole.Prompt(prompt);
        files = dict[directoryInfo];
    }
    else
    {
        files = finder.GetScripts();
    }

    if (files.Length == 0)
    {
        AnsiConsole.Markup($"[red]No scripts script files found in '{finder.RootDirectory}' with extensions '{string.Join(", ", finder.Extensions)}'[/]");
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
        .UseConverter(PromptDecorator.FileStyle)
        .HighlightStyle(PromptDecorator.SelectionHighlight)
        .AddChoices(files);

        var scripts = AnsiConsole.Prompt(prompt);

        await ScriptExecutor.ExecAsync(scripts, elevated);
    }
    else
    {
        var prompt = new SelectionPrompt<FileInfo>()
        .Title("Select a script to execute:")
        .PageSize(ScriptListSize)
        .MoreChoicesText("[grey]Move up and down to reveal more options[/]")
        .UseConverter(PromptDecorator.FileStyle)
        .HighlightStyle(PromptDecorator.SelectionHighlight)
        .AddChoices(files);

        var script = AnsiConsole.Prompt(prompt);

        await ScriptExecutor.ExecAsync(script, elevated);
    }
}

static class PromptDecorator
{
    internal static string FileStyle(FileInfo info)
    {
        var filename = Path.GetFileNameWithoutExtension(info.Name);
        var extension = Path.GetExtension(info.Name);

        return $"[blue]{info.DirectoryName}{Path.DirectorySeparatorChar}[/][orangered1]{filename}[/][greenyellow]{extension}[/]";
    }

    internal static string DirectoryStyle(DirectoryInfo info) => $"[blue]{info}[/]";

    internal static Style SelectionHighlight => new(decoration: Decoration.Bold | Decoration.Underline);
}

static class ScriptExecutor
{
    internal static async Task ExecAsync(List<FileInfo> files, bool elevated)
    {
        await Parallel.ForEachAsync(files, (x, ct) => ExecAsync(x, elevated, ct));
    }

    internal static async ValueTask ExecAsync(FileInfo file, bool elevated, CancellationToken cancellationToken = default)
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
    public string[] Extensions { get; }
    public string RootDirectory { get; }
    public int Depth { get; }
    private readonly EnumerationOptions _options;

    public ScriptFinder(string? extensions, string directory, int depth)
    {
        Extensions = extensions?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet()
        .Select(x => $".{x.TrimStart('.')}")
        .ToArray() ?? new[] { ".ps1", ".*sh", ".bat", ".cmd" };

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

    internal FileInfo[] GetScripts() =>
        Extensions.Select(GetScriptFilesWithExtension).SelectMany(x => x).ToArray();

    internal IDictionary<DirectoryInfo, FileInfo[]> GetScriptsByDirectory() =>
        Extensions
        .Select(GetScriptFilesWithExtension)
        .SelectMany(x => x)
        .GroupBy(x => x.DirectoryName!)
        .OrderBy(x => x.Key)
        .ToDictionary(x => new DirectoryInfo(x.Key), x => x.ToArray());
}
