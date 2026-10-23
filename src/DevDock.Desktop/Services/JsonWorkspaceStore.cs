using System.Text.Json;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class JsonWorkspaceStore(string filePath) : IWorkspaceStore
{
    private const int MaxBytes = 4 * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath = System.IO.Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private sealed record WorkspaceDocument(int Version, SavedProject[] Projects);

    public static string DefaultFilePath => System.IO.Path.Combine(
        OperatingSystem.IsMacOS()
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevDock", "workspace.json");

    public async Task<IReadOnlyList<SavedProject>> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run<IReadOnlyList<SavedProject>>(async () =>
            {
                FileStream stream;
                try { stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous); }
                catch (FileNotFoundException) { return []; }
                catch (DirectoryNotFoundException) { return []; }
                await using (stream)
                {
                    if (stream.Length > MaxBytes) throw new IOException("The saved workspace is too large to read.");
                    var document = await JsonSerializer.DeserializeAsync<WorkspaceDocument>(stream);
                    if (document is null || document.Version != 1 || document.Projects is null)
                        throw new InvalidDataException("The saved workspace format is not supported.");
                    return Validate(document.Projects);
                }
            });
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(IReadOnlyList<SavedProject> projects)
    {
        var snapshot = Validate(projects);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new WorkspaceDocument(1, snapshot), Options);
        if (bytes.Length > MaxBytes) throw new IOException("The workspace is too large to save.");
        await _gate.WaitAsync();
        try
        {
            await Task.Run(async () =>
            {
                var directory = System.IO.Path.GetDirectoryName(_filePath)!;
                Directory.CreateDirectory(directory);
                var temporary = System.IO.Path.Combine(directory, $".workspace-{Guid.NewGuid():N}.tmp");
                try
                {
                    await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        await stream.WriteAsync(bytes);
                        await stream.FlushAsync();
                        stream.Flush(flushToDisk: true);
                    }
                    // Same-directory replacement: a failed write leaves the previous workspace intact.
                    File.Move(temporary, _filePath, overwrite: true);
                }
                finally
                {
                    try { File.Delete(temporary); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Trace.WriteLine(ex); }
                }
            });
        }
        finally { _gate.Release(); }
    }

    private static SavedProject[] Validate(IReadOnlyList<SavedProject> projects)
    {
        if (projects.Count > 2000) throw new InvalidDataException("The workspace contains too many projects.");
        var paths = new HashSet<string>(ProjectPath.Comparer);
        var ids = new HashSet<Guid>();
        var result = new List<SavedProject>();
        foreach (var entry in projects)
        {
            if (entry is null || entry.Id == Guid.Empty || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Path)
                || !System.IO.Path.IsPathFullyQualified(entry.Path) || entry.Technologies is null || entry.Technologies.Any(t => t is null))
                throw new InvalidDataException("The saved workspace contains an invalid project entry.");
            var path = ProjectPath.Normalize(entry.Path);
            if (!paths.Add(path)) continue;
            result.Add(entry with { Path = path, Id = ids.Add(entry.Id) ? entry.Id : Guid.NewGuid(), Technologies = entry.Technologies.ToArray() });
        }
        return result.ToArray();
    }
}
