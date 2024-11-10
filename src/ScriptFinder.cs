namespace ScriptLauncher;

internal readonly struct ScriptFinder
{
    public IEnumerable<string> Extensions { get; }
    public string RootDirectory { get; }
    private int Depth { get; }

    private readonly EnumerationOptions _options;

    public ScriptFinder(IEnumerable<string> extensions, string directory, int depth)
    {
        Extensions = extensions.ToHashSet().Select(static x => $".{x.TrimStart('.')}");

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
            return filenames.Select(static x => new FileInfo(x));
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<FileInfo>();
        }
    }

    public FileInfo[] GetScripts() =>
        Extensions.Select(GetScriptFilesWithExtension).SelectMany(static x => x).ToArray();

    public IDictionary<DirectoryInfo, FileInfo[]> GetScriptsByDirectory() =>
        Extensions
            .Select(GetScriptFilesWithExtension)
            .SelectMany(static x => x)
            .GroupBy(static x => x.DirectoryName!)
            .OrderBy(static x => x.Key)
            .ToDictionary(static x => new DirectoryInfo(x.Key), static x => x.ToArray());
}
