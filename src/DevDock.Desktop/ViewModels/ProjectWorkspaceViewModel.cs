using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDock.Desktop.Models;
using DevDock.Desktop.Services;

namespace DevDock.Desktop.ViewModels;

public partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly IProjectRunnerService _runner;
    private readonly IProjectRunConfigurationService _configurations;
    private readonly IProjectWorkspaceInteraction? _interaction;
    private ProjectProcess? _process;
    private ProjectRunConfiguration? _chosenConfiguration;
    private long _lastOutputSequence;
    private int _updatePending;
    public ProjectViewModel Project { get; }
    public GitWorkspaceViewModel Git { get; }
    public ObservableCollection<ProjectOutputLine> Output { get; } = [];
    public ProjectProcess? Process => _process;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _operation = "";
    public bool HasError => Error.Length > 0;
    public bool HasOutput => Output.Count > 0;
    public string RunLabel => IsBusy ? "Working…" : Project.IsRunning ? "■  Stop" : "▶  Run";
    public string RunState => Project.IsRunning ? "Running · started by DevDock" : _process is null ? "Not started by DevDock" : $"Stopped · exit {_process.ExitCode}";
    public string LaunchSummary => _chosenConfiguration?.DisplayCommand ?? "DevDock will inspect runnable projects and existing start/dev/serve scripts when you click Run.";
    private bool CanRun => !IsBusy && !Project.IsRemoving;
    private bool CanChoose => CanRun && !Project.IsRunning;
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => UpdateCommands();

    public ProjectWorkspaceViewModel(ProjectViewModel project, IGitService git, IProjectRunnerService runner,
        IProjectRunConfigurationService configurations, IProjectWorkspaceInteraction? interaction)
    {
        Project = project; Git = new(git, project); _runner = runner; _configurations = configurations; _interaction = interaction;
        project.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(project.IsRunning) or nameof(project.IsRemoving)) UpdateCommands(); };
    }

    private void UpdateCommands()
    {
        OnPropertyChanged(nameof(RunLabel)); OnPropertyChanged(nameof(RunState));
        RunStopCommand.NotifyCanExecuteChanged(); ChooseLaunchTargetCommand.NotifyCanExecuteChanged();
    }

    private async Task<ProjectRunConfiguration?> ResolveConfigurationAsync(bool chooseAgain = false)
    {
        var configurations = await _configurations.DetectAsync(Project.Id, Project.Path);
        var previous = _chosenConfiguration is { } chosen ? configurations.FirstOrDefault(c => c.Name == chosen.Name && c.DisplayCommand == chosen.DisplayCommand) : null;
        if (!chooseAgain && previous is not null) return previous;
        if (configurations.Count == 0) throw new InvalidOperationException("DevDock could not automatically determine how to run this project. No supported runnable .NET project or start/dev/serve script was found. Java launchers are supported only when an explicit known configuration and native tool are available.");
        if (configurations.Count == 1) return configurations[0];
        if (_interaction is null) throw new InvalidOperationException("Choose a launch target from the DevDock desktop window.");
        return await _interaction.ChooseRunConfigurationAsync(Project.Name, configurations);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunStopAsync()
    {
        IsBusy = true; Error = "";
        try
        {
            if (_process?.IsRunning == true) { Operation = "Stopping…"; await _runner.StopAsync(_process); SyncProcess(); return; }
            Operation = "Detecting launch configuration…";
            var configuration = await ResolveConfigurationAsync();
            if (configuration is null) { Operation = "Run cancelled."; return; }
            _chosenConfiguration = configuration; OnPropertyChanged(nameof(LaunchSummary));
            if (_process is not null) _process.Updated -= QueueProcessUpdate;
            SelectedTabIndex = 2; Output.Clear(); _lastOutputSequence = 0;
            Operation = "Starting…";
            _process = await _runner.StartAsync(configuration);
            _process.Updated += QueueProcessUpdate;
            SyncProcess();
            Operation = "Project launched. Output is captured below.";
        }
        catch (Exception ex) { Error = "Unable to run/stop project. " + ex.Message; Operation = "Project operation failed. View Output for diagnostics."; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task ChooseLaunchTargetAsync()
    {
        IsBusy = true; Error = "";
        try
        {
            var configuration = await ResolveConfigurationAsync(chooseAgain: true);
            if (configuration is not null) { _chosenConfiguration = configuration; OnPropertyChanged(nameof(LaunchSummary)); }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void QueueProcessUpdate()
    {
        if (Interlocked.Exchange(ref _updatePending, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { Interlocked.Exchange(ref _updatePending, 0); SyncProcess(); });
    }

    private void SyncProcess()
    {
        if (_process is null) return;
        Project.IsRunning = _process.IsRunning;
        foreach (var line in _process.GetOutput().Where(l => l.Sequence > _lastOutputSequence))
        {
            Output.Add(line); _lastOutputSequence = line.Sequence;
        }
        while (Output.Count > 2000) Output.RemoveAt(0);
        if (!_process.IsRunning && _process.ExitCode != 0) Error = $"Project exited with code {_process.ExitCode}. View Output for details.";
        OnPropertyChanged(nameof(Process)); OnPropertyChanged(nameof(HasOutput)); UpdateCommands();
    }

    [RelayCommand] private void ClearOutput() { _process?.ClearOutput(); Output.Clear(); OnPropertyChanged(nameof(HasOutput)); }

    public async Task StopForRemovalAsync()
    {
        if (IsBusy) throw new InvalidOperationException("Wait for the current project operation before removing it.");
        if (_process is not null)
        {
            await _runner.StopAsync(_process);
            _process.Updated -= QueueProcessUpdate;
            SyncProcess();
        }
    }
}
