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
    public void Dispose() => Directory.Delete(Root, recursive: true);
}
