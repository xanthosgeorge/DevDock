namespace DevDock.Desktop.Services;

public static class ProjectPath
{
    // Unix paths remain case-sensitive, including case-sensitive macOS volumes.
    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
