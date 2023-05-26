using System.Diagnostics;

namespace ScriptLauncher;

internal static class ScriptExecutor
{
    public static async Task ExecAsync(List<FileInfo> files, bool elevated) => 
        await Parallel.ForEachAsync(files, (x, ct) => ExecAsync(x, elevated, ct));

    private static async ValueTask ExecAsync(FileInfo file, bool elevated, CancellationToken cancellationToken = default)
    {
        var process = GetExecutableProcessInfo(file, elevated);
        if (process is null)
        {
            return;
        }
        
        await (Process.Start(process)?.WaitForExitAsync(cancellationToken) ?? Task.CompletedTask);
    }

    private static ProcessStartInfo? GetExecutableProcessInfo(FileInfo file, bool elevated) => file.Extension switch
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
        ".sh" or ".zsh" or ".fish" => new ProcessStartInfo
        {
            FileName = "sh",
            Arguments = $"-c ./{file.Name}",
            Verb = elevated ? "sudo" : string.Empty,
            WorkingDirectory = file.DirectoryName
        },
        var _ => null
    };
}