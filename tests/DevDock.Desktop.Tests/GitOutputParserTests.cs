using DevDock.Desktop.Services;
using Xunit;

namespace DevDock.Desktop.Tests;

public sealed class GitOutputParserTests
{
    [Fact]
    public void ParsesPorcelainV2HeadersChangesRenamesAndConflicts()
    {
        var output = "# branch.oid abc123\0# branch.head feature/work\0# branch.upstream origin/main\0# branch.ab +2 -1\0"
            + "1 MM N... 100644 100644 100644 a b src/a file.cs\0"
            + "2 R. N... 100644 100644 100644 a b R100 new ü.txt\0old name.txt\0"
            + "? --untracked.txt\0u UU N... 100644 100644 100644 100644 a b c conflict.cs\0";
        var status = GitOutputParser.ParseStatus(output, "root");
        Assert.Equal("feature/work", status.Branch);
        Assert.Equal("origin/main", status.Upstream);
        Assert.Equal(2, status.Ahead); Assert.Equal(1, status.Behind);
        Assert.Equal(4, status.Files.Count);
        Assert.Equal("src/a file.cs", status.Files[0].Path);
        Assert.True(status.Files[0].IsStaged); Assert.True(status.Files[0].IsUnstaged);
        Assert.Equal("old name.txt", status.Files[1].OriginalPath);
        Assert.True(status.Files[2].IsUntracked); Assert.False(status.Files[2].IsStaged);
        Assert.True(status.HasConflicts);
    }

    [Fact]
    public void HandlesUnbornDetachedAndNewlineFilenames()
    {
        var unborn = GitOutputParser.ParseStatus("# branch.oid (initial)\0# branch.head main\0? new\nfile.txt\0", "root");
        Assert.Null(unborn.HeadCommit);
        Assert.Equal("new\nfile.txt", Assert.Single(unborn.Files).Path);
        var detached = GitOutputParser.ParseStatus("# branch.head (detached)\0# future.header ignored\0", "root");
        Assert.Equal("Detached HEAD", detached.Branch);
    }

    [Fact]
    public void ParsesNullDelimitedHistoryWithHeadAndDates()
    {
        var history = GitOutputParser.ParseHistory("abcdef\0abc\0Add login | Δοκιμή\0George\02026-10-23T10:42:00+03:00\0"
            + "123456\0123\0Initial commit\0Other\02026-10-22T08:00:00Z\0");
        Assert.Equal(2, history.Count);
        Assert.True(history[0].IsHead); Assert.False(history[1].IsHead);
        Assert.Equal("Add login | Δοκιμή", history[0].Message);
        Assert.Equal(TimeSpan.FromHours(3), history[0].Date.Offset);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git", "https://github.com/owner/repo.git")]
    [InlineData("git@github.com:owner/repo.git", "git@github.com:owner/repo.git")]
    [InlineData("ssh://git@github.com/owner/repo.git", "ssh://git@github.com/owner/repo.git")]
    [InlineData("https://user:token@github.com/owner/repo.git", "https://[redacted]@github.com/owner/repo.git")]
    public void PreservesRemoteFormatsAndHidesHttpCredentials(string url, string expected)
    {
        var remote = Assert.Single(GitOutputParser.ParseRemotes("remote.origin.url\n" + url + "\0"));
        Assert.Equal("origin", remote.Name);
        Assert.Equal(expected, remote.DisplayUrl);
    }
}
