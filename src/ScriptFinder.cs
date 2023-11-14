namespace ScriptLauncher;

internal readonly struct ScriptFinder
{
    private static readonly string[] DefaultExtensions = new[] { ".ps1", ".*sh", ".bat", ".cmd" };
    private static readonly char[] DefaultSeparators = new[] { ',', ' ' };

    public string[] Extensions { get; }
    public string RootDirectory { get; }
    public int Depth { get; }

    private readonly EnumerationOptions _options;

    public ScriptFinder(string? extensions, string directory, int depth)
    {
        Extensions =
            extensions
                ?.Split(DefaultSeparators, StringSplitOptions.RemoveEmptyEntries)
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
