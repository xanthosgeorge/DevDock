using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public interface IGitService
{
    Task<GitRepositoryStatus> GetStatusAsync(string repositoryPath);
    Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string repositoryPath, int count = 50);
    Task FetchAsync(string repositoryPath);
    Task PullAsync(string repositoryPath);
    Task StageAsync(string repositoryPath, IEnumerable<string> files);
    Task StageAllAsync(string repositoryPath);
    Task UnstageAsync(string repositoryPath, IEnumerable<string> files);
    Task CommitAsync(string repositoryPath, string message);
}
