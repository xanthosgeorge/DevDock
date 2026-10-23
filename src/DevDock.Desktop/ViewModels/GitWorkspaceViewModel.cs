using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public partial class GitChangeViewModel(GitFileChange change, bool staged, Action selectionChanged) : ObservableObject
{
    public GitFileChange Change { get; } = change;
    public string Path => Change.Path;
    public string Code => staged ? Change.IndexLabel : Change.WorkTreeLabel;
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool value) => selectionChanged();
}

public partial class GitWorkspaceViewModel : ObservableObject
{
    private readonly IGitService service;
    private readonly ProjectViewModel project;
    public GitWorkspaceViewModel(IGitService service, ProjectViewModel project)
    {
        this.service = service; this.project = project;
        project.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(project.IsRemoving)) UpdateCommands(); };
    }
    private GitRepositoryStatus _status = new();
    private int _historyLimit = 50;
    public ObservableCollection<GitChangeViewModel> Changes { get; } = [];
    public ObservableCollection<GitChangeViewModel> Staged { get; } = [];
    public ObservableCollection<GitCommit> History { get; } = [];
    public ObservableCollection<GitRemote> Remotes { get; } = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRepository;
    [ObservableProperty] private bool _gitAvailable = true;
    [ObservableProperty] private string _branch = "";
    [ObservableProperty] private string _summary = "Repository information has not been loaded.";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _commitMessage = "";
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool IsNotRepository => !IsRepository && GitAvailable && !IsBusy;
    public string ChangesHeading => $"Changes ({Changes.Count})";
    public string StagedHeading => $"Staged changes ({Staged.Count})";
    public string HistoryHeading => $"History · latest {History.Count}";
    public bool IsHistoryEmpty => History.Count == 0;
    public bool HasChanges => Changes.Count > 0;
    public bool HasStaged => Staged.Count > 0;
    private bool CanUseGit => !IsBusy && IsRepository && GitAvailable && !project.IsRemoving;
    private bool CanRefresh => !IsBusy && !project.IsRemoving;
    private bool CanStageAll => CanUseGit && Changes.Count > 0;
    private bool CanStageSelected => CanUseGit && Changes.Any(c => c.IsSelected);
    private bool CanUnstageSelected => CanUseGit && Staged.Any(c => c.IsSelected);
    private bool CanCommit => CanUseGit && Staged.Count > 0 && !_status.HasConflicts && !string.IsNullOrWhiteSpace(CommitMessage);
    public bool CanLoadMore => CanUseGit && History.Count >= _historyLimit && _historyLimit < 500;
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnCommitMessageChanged(string value) => CommitCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) { UpdateCommands(); OnPropertyChanged(nameof(IsNotRepository)); }
    partial void OnIsRepositoryChanged(bool value) { UpdateCommands(); OnPropertyChanged(nameof(IsNotRepository)); }
    partial void OnGitAvailableChanged(bool value) { UpdateCommands(); OnPropertyChanged(nameof(IsNotRepository)); }

    private void UpdateCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged(); FetchCommand.NotifyCanExecuteChanged(); PullCommand.NotifyCanExecuteChanged();
        StageFileCommand.NotifyCanExecuteChanged(); UnstageFileCommand.NotifyCanExecuteChanged();
        StageSelectedCommand.NotifyCanExecuteChanged(); StageAllCommand.NotifyCanExecuteChanged();
        UnstageSelectedCommand.NotifyCanExecuteChanged(); CommitCommand.NotifyCanExecuteChanged(); LoadMoreCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanLoadMore));
    }

    private async Task RefreshCoreAsync()
    {
        _status = await service.GetStatusAsync(project.Path);
        GitAvailable = true;
        IsRepository = _status.IsRepository;
        Branch = _status.Branch;
        Summary = IsRepository ? $"{_status.Summary} · ↑ {_status.Ahead}  ↓ {_status.Behind}" : "This project is not a Git repository.";
        project.ApplyGitStatus(_status);
        Changes.Clear(); Staged.Clear(); Remotes.Clear(); History.Clear();
        foreach (var file in _status.Files)
        {
            if (file.IsUnstaged) Changes.Add(new(file, false, UpdateCommands));
            if (file.IsStaged) Staged.Add(new(file, true, UpdateCommands));
        }
        foreach (var remote in _status.Remotes) Remotes.Add(remote);
        if (IsRepository)
            foreach (var commit in await service.GetHistoryAsync(_status.RootPath, _historyLimit)) History.Add(commit);
        foreach (var name in new[] { nameof(ChangesHeading), nameof(StagedHeading), nameof(HistoryHeading), nameof(IsHistoryEmpty), nameof(HasChanges), nameof(HasStaged) }) OnPropertyChanged(name);
        UpdateCommands();
    }

    private async Task PerformAsync(string progress, Func<Task>? action = null, string completed = "Git information refreshed.", bool commit = false)
    {
        if (IsBusy) return;
        IsBusy = true; Error = ""; Message = progress;
        try
        {
            if (action is not null) await action();
            await RefreshCoreAsync();
            Message = commit && History.Count > 0 ? $"Commit created successfully. {History[0].ShortHash} · {History[0].Message}" : completed;
            if (commit) CommitMessage = "";
        }
        catch (GitUnavailableException ex)
        {
            GitAvailable = false; IsRepository = false; Error = ex.Message; Message = "Git unavailable";
            project.GitStatus = ex.Message; project.Branch = "";
        }
        catch (Exception ex)
        {
            Error = GitOutputParser.RedactCredentials(ex.Message); Message = "Git operation failed";
            // A failed pull/hook can leave useful new state. Preserve the original error while refreshing it.
            if (action is not null) try { await RefreshCoreAsync(); } catch { }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))] private Task RefreshAsync() => PerformAsync("Refreshing Git…");
    [RelayCommand(CanExecute = nameof(CanUseGit))] private Task FetchAsync() => PerformAsync("Fetching…", () => service.FetchAsync(_status.RootPath), "Fetch completed.");
    [RelayCommand(CanExecute = nameof(CanUseGit))] private Task PullAsync() => PerformAsync("Pulling…", () => service.PullAsync(_status.RootPath), "Pull completed.");
    [RelayCommand(CanExecute = nameof(CanUseGit))] private Task StageFileAsync(GitChangeViewModel file) => PerformAsync("Staging…", () => service.StageAsync(_status.RootPath, [file.Path]), "File staged.");
    [RelayCommand(CanExecute = nameof(CanUseGit))] private Task UnstageFileAsync(GitChangeViewModel file) => PerformAsync("Unstaging…", () => service.UnstageAsync(_status.RootPath, [file.Path]), "File unstaged. Working files were kept.");
    [RelayCommand(CanExecute = nameof(CanStageSelected))] private Task StageSelectedAsync()
    {
        var paths = Changes.Where(c => c.IsSelected).Select(c => c.Path).ToArray();
        return PerformAsync("Staging…", () => service.StageAsync(_status.RootPath, paths), "Selected files staged.");
    }
    [RelayCommand(CanExecute = nameof(CanStageAll))] private Task StageAllAsync() => PerformAsync("Staging…", () => service.StageAllAsync(_status.RootPath), "All changes staged.");
    [RelayCommand(CanExecute = nameof(CanUnstageSelected))] private Task UnstageSelectedAsync()
    {
        var paths = Staged.Where(c => c.IsSelected).Select(c => c.Path).ToArray();
        return PerformAsync("Unstaging…", () => service.UnstageAsync(_status.RootPath, paths), "Selected files unstaged. Working files were kept.");
    }
    [RelayCommand(CanExecute = nameof(CanCommit))] private Task CommitAsync() => PerformAsync("Committing…", () => service.CommitAsync(_status.RootPath, CommitMessage), commit: true);
    [RelayCommand(CanExecute = nameof(CanLoadMore))] private Task LoadMoreAsync() { _historyLimit += 50; return PerformAsync("Loading history…"); }
}
