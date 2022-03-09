using System.Diagnostics;
using Cocona;
using Spectre.Console;

var app = CoconaLiteApp.Create();

app.AddCommand(RootCommand);
app.Run();

static void RootCommand(
    [Option("dir", new[] { 'd' }, Description = "Directory from which search the scripts")] string directory = ".",
    [Option("ext", Description = "Comma separated list of script extensions to search")] string extensions = "*",
    [Option("elevated", new[] { 'e' }, Description = "Run the script with elevated privileges")] bool elevated = false,
    [Option("depth")] int depth = 0)
{
    if (!Directory.Exists(directory))
    {
        AnsiConsole.Markup($"[red]The directory {directory} does not exist.[/]");
        Environment.Exit(1);
    }

    var fileExtensions = extensions.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet()
        .ToArray();

    var finder = new ScriptFinder()
    {
        Depth = depth,
        Extensions = fileExtensions,
        RootFolder = directory,
    };

    var files = finder.GetScriptFiles(fileExtensions);

    if (files.Length == 0)
    {
        AnsiConsole.Markup($"[red]No scripts script files found in {directory} with extensions '{string.Join("|", fileExtensions)}'[/]");
        Environment.Exit(1);
    }

    var prompt = new SelectionPrompt<string>()
        .Title("Select a script")
        .AddChoices(files.Select(x => x.FullName));

    var script = AnsiConsole.Prompt(prompt);        

    ScriptExecutor.Exec(script);
};

static class ScriptExecutor
{
    internal static void Exec(string filename)
    {
        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-File {filename}",
            UseShellExecute = false,
        };

        Exec(info);
    }

    internal static void Exec(ProcessStartInfo info) => Process.Start(info)?.WaitForExit();
}

class ScriptFinder
{
    public string RootFolder { get; set; } = ".";
    public string[] Extensions { get; set; } = new[] { "ps1", "*sh", "bat", "cmd" };
    public int Depth { get; set; } = 0;

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

    internal FileInfo[] GetScriptFiles(string extension)
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

    internal FileInfo[] GetScriptFiles(string[] extensions) => 
        extensions.Select(GetScriptFiles).SelectMany(x => x).ToArray();
}