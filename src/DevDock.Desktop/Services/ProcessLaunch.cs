using System.Diagnostics;
using System.Text;

namespace DevDock.Desktop.Services;

/// <summary>All command arguments stay separate from the executable. No repository shell text is evaluated.</summary>
public static class ProcessLaunch
{
    public static ProcessStartInfo Create(string executable, IEnumerable<string> arguments, string workingDirectory)
    {
        var args = arguments.ToArray();
        var info = new ProcessStartInfo
        {
            FileName = executable, WorkingDirectory = workingDirectory,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        if (OperatingSystem.IsWindows() && executable == "npm")
        {
            // npm is normally a .cmd shim on Windows. Invoke its installed JS CLI through node instead.
            var cli = PathDirectories().Select(p => Path.Combine(p, "node_modules", "npm", "bin", "npm-cli.js"))
                .FirstOrDefault(File.Exists);
            if (cli is null) throw new InvalidOperationException("npm was not found. Install Node.js/npm and restart DevDock.");
            info.FileName = FindTool("node.exe") ?? throw new InvalidOperationException("Node.js was not found on this system.");
            info.ArgumentList.Add(cli);
        }
        if (OperatingSystem.IsWindows() && executable is "mvn" or "gradle")
            throw new NotSupportedException("DevDock could not automatically determine how to run this project with a native Java launcher on Windows. Maven/Gradle .cmd and repository wrappers are not executed.");
        foreach (var argument in args) info.ArgumentList.Add(argument);
        return info;
    }

    private static IEnumerable<string> PathDirectories() => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(p => p.Trim('"')).Where(p => Path.IsPathFullyQualified(p));

    private static string? FindTool(string name) => PathDirectories().Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
}
