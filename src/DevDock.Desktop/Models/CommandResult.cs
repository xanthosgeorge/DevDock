namespace DevDock.Desktop.Models;

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr, bool OutputTruncated = false);
