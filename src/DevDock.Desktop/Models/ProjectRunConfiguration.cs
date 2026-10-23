namespace DevDock.Desktop.Models;

public sealed record ProjectRunConfiguration(Guid ProjectId, string Name, string Executable,
    IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    public string DisplayCommand => Executable + " " + string.Join(" ", Arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
}
