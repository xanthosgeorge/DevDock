namespace DevDock.Desktop.Tests;

internal sealed class TemporaryProject : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "DevDock.Tests", Guid.NewGuid().ToString("N"));
    public TemporaryProject() => Directory.CreateDirectory(Root);
    public void Write(string relative, string content = "")
    {
        var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
    public void Dispose()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DevDock.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fixture cleanup escaped its temporary root.");
        foreach (var file in Directory.EnumerateFiles(Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        Directory.Delete(Root, recursive: true);
    }
}
