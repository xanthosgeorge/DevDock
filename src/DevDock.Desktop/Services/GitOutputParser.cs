using System.Globalization;
using System.Text.RegularExpressions;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public static class GitOutputParser
{
    public static GitRepositoryStatus ParseStatus(string output, string root, IReadOnlyList<GitRemote>? remotes = null)
    {
        string branch = "", head = "", upstream = "";
        var ahead = 0; var behind = 0;
        var files = new List<GitFileChange>();
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.StartsWith("# branch.head ", StringComparison.Ordinal)) branch = record[14..];
            else if (record.StartsWith("# branch.oid ", StringComparison.Ordinal)) head = record[13..];
            else if (record.StartsWith("# branch.upstream ", StringComparison.Ordinal)) upstream = record[18..];
            else if (record.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var counts = record[12..].Split(' ');
                if (counts.Length == 2) { int.TryParse(counts[0].TrimStart('+'), out ahead); int.TryParse(counts[1].TrimStart('-'), out behind); }
            }
            else if (record.StartsWith("? ", StringComparison.Ordinal)) files.Add(new(record[2..], '?', '?'));
            else if (record.Length > 2 && record[0] is '1' or '2' or 'u')
            {
                var fields = record.Split(' ', record[0] == '1' ? 9 : record[0] == '2' ? 10 : 11);
                var expected = record[0] == '1' ? 9 : record[0] == '2' ? 10 : 11;
                if (fields.Length != expected || fields[1].Length != 2) throw new FormatException("Git returned an invalid status entry.");
                string? original = null;
                if (record[0] == '2') original = ++index < records.Length ? records[index] : throw new FormatException("Git returned an incomplete rename entry.");
                files.Add(new(fields[^1], fields[1][0], fields[1][1], original, record[0] == 'u'));
            }
        }
        return new()
        {
            IsRepository = true, RootPath = root, Branch = branch == "(detached)" ? "Detached HEAD" : branch,
            HeadCommit = head == "(initial)" ? null : head, Upstream = string.IsNullOrEmpty(upstream) ? null : upstream,
            Ahead = ahead, Behind = behind, Files = files, Remotes = remotes ?? []
        };
    }

    public static IReadOnlyList<GitCommit> ParseHistory(string output)
    {
        var fields = output.Split('\0');
        var commits = new List<GitCommit>();
        for (var index = 0; index + 4 < fields.Length; index += 5)
        {
            var hash = fields[index].TrimStart('\r', '\n');
            if (hash.Length == 0) break;
            commits.Add(new(hash, fields[index + 1], fields[index + 2], fields[index + 3],
                DateTimeOffset.Parse(fields[index + 4], CultureInfo.InvariantCulture), commits.Count == 0));
        }
        return commits;
    }

    public static IReadOnlyList<GitRemote> ParseRemotes(string output) => output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Select(record => record.Split('\n', 2)).Where(parts => parts.Length == 2 && parts[0].StartsWith("remote.", StringComparison.Ordinal) && parts[0].EndsWith(".url", StringComparison.Ordinal))
        .Select(parts => new GitRemote(parts[0][7..^4], RedactCredentials(parts[1]))).ToArray();

    public static string RedactCredentials(string text) => Regex.Replace(text, @"(https?://)[^\s/@]+(?::[^\s/@]*)?@", "$1[redacted]@", RegexOptions.IgnoreCase);
}
