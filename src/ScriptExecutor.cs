using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;

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
