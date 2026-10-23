using System.Text.Json;
using System.Text.RegularExpressions;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public static class RuntimeEndpointDetector
{
    public static ProjectEndpoint? Parse(string text)
    {
        text = Regex.Replace(text, @"\x1B\[[0-?]*[ -/]*[@-~]", "");
        var match = Regex.Match(text, @"(?:Now listening on:\s*|(?:^|\s)Local:\s*)(https?://[^\s]+)", RegexOptions.IgnoreCase);
        if (match.Success && Validate(match.Groups[1].Value.TrimEnd(',', ';'), out var url))
            return new(text.Contains("Local:", StringComparison.OrdinalIgnoreCase) ? "Frontend" : "Application", url!, true);
        // Spring Boot's embedded server startup message, not arbitrary mentions of URLs/ports.
        match = Regex.Match(text, @"Tomcat started on port(?:\(s\))?\s*:?\s*(\d+)\s*\((https?)\)(?:.*?context path '([^']*)')?", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var port) && port is > 0 and <= 65535)
        {
            var path = match.Groups[3].Value;
            if (!path.StartsWith('/')) path = "/";
            if (Validate($"{match.Groups[2].Value}://localhost:{port}{path}", out url)) return new("Application", url!, true);
        }
        return null;
    }

    public static bool Validate(string value, out Uri? url)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out url) && url.Scheme is "http" or "https"
            && url.Host.Length > 0 && url.UserInfo.Length == 0 && url.Port is > 0 and <= 65535) return true;
        url = null; return false;
    }

    public static async Task<IReadOnlyList<ProjectEndpoint>> ReadExpectedAsync(ProjectRunConfiguration configuration)
    {
        if (configuration.Executable != "dotnet" || !configuration.Arguments.Contains("--project")) return [];
        var path = Path.Combine(configuration.WorkingDirectory, "Properties", "launchSettings.json");
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > ProjectDiscoveryService.MaxManifestBytes) return [];
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("profiles", out var profiles)) return [];
            // dotnet run defaults to the first Project profile; IIS Express is not a dotnet launch target.
            foreach (var profile in profiles.EnumerateObject())
            {
                if (!profile.Value.TryGetProperty("commandName", out var command) || command.GetString() != "Project") continue;
                if (!profile.Value.TryGetProperty("applicationUrl", out var urls) || urls.ValueKind != JsonValueKind.String) return [];
                return (urls.GetString() ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(value => Validate(value, out _)).Select(value => new ProjectEndpoint("Expected application", new Uri(value), false)).DistinctBy(e => e.Url).ToArray();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { System.Diagnostics.Trace.WriteLine(ex); }
        return [];
    }
}
