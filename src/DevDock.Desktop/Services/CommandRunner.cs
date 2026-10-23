using System.Diagnostics;
using System.Text;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class CommandRunner : ICommandRunner
{
    public async Task<CommandResult> ExecuteAsync(string executable, IEnumerable<string> arguments, string workingDirectory,
        string? standardInput = null, CancellationToken cancellationToken = default)
    {
        var info = ProcessLaunch.Create(executable, arguments, workingDirectory);
        info.RedirectStandardInput = true;
        // Existing cached credentials/SSH config remain in use; no terminal password prompts.
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["GCM_INTERACTIVE"] = "Never";
        info.Environment["LC_ALL"] = "C";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        using var process = new Process { StartInfo = info };
        process.Start();
        var stdout = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var stderr = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            if (standardInput is not null) await process.StandardInput.WriteAsync(standardInput.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            return new(process.ExitCode, output.Text, error.Text, output.Truncated || error.Truncated);
        }
        catch
        {
            // Only this command's own process tree is terminated on timeout/cancellation.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
            throw;
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        const int limit = 4 * 1024 * 1024;
        var text = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            var keep = Math.Min(read, limit - text.Length);
            text.Append(buffer, 0, keep);
            truncated |= keep < read;
        }
        return (text.ToString(), truncated);
    }
}
