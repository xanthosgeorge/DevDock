using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface ICommandRunner
{
    Task<CommandResult> ExecuteAsync(string executable, IEnumerable<string> arguments, string workingDirectory,
        string? standardInput = null, CancellationToken cancellationToken = default);
}
