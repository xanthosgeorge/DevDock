using DevDock.Desktop.Models;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.Tests;

internal sealed class TemporaryGitRepository : IDisposable
{
    public TemporaryProject Files { get; } = new();
    public string Root => Path.Combine(Files.Root, "repository");
    public ICommandRunner Commands { get; } = new CommandRunner();
    public GitCliService Git { get; }
    public TemporaryGitRepository() { Directory.CreateDirectory(Root); Git = new(Commands); }
    public async Task InitializeAsync()
    {
        await NativeAsync(Root, "init", "--initial-branch=main");
        await NativeAsync(Root, "config", "user.name", "DevDock Tests");
        await NativeAsync(Root, "config", "user.email", "devdock-tests@example.invalid");
        await NativeAsync(Root, "config", "commit.gpgsign", "false");
        await NativeAsync(Root, "config", "core.autocrlf", "false");
        await NativeAsync(Root, "config", "core.hooksPath", Path.Combine(Files.Root, "no-hooks"));
    }
    public void Write(string name, string content) => Files.Write("repository/" + name, content);
    public async Task<CommandResult> NativeAsync(string cwd, params string[] args)
    {
        var result = await Commands.ExecuteAsync("git", args, cwd);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StdErr);
        return result;
    }
    public void Dispose() => Files.Dispose();
}
