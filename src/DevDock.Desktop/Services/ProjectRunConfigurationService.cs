using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

public sealed class ProjectRunConfigurationService(IProjectDiscoveryService discovery) : IProjectRunConfigurationService
{
    public async Task<IReadOnlyList<ProjectRunConfiguration>> DetectAsync(Guid projectId, string rootPath)
    {
        var result = await discovery.DiscoverAsync(rootPath);
        return await Task.Run(() => Detect(projectId, result));
    }

    private static IReadOnlyList<ProjectRunConfiguration> Detect(Guid projectId, ProjectDiscoveryResult result)
    {
        var configurations = new List<ProjectRunConfiguration>();
        foreach (var file in result.ProjectFiles)
        {
            if (file.Kind is not (".NET project" or "Node.js project" or "Maven project" or "Gradle project")) continue;
            var path = Path.Combine(result.RootPath, file.RelativePath);
            try
            {
                // Read bounded contents again, so a file changed since import cannot become an unbounded read.
                using var stream = File.OpenRead(path);
                if (stream.Length > ProjectDiscoveryService.MaxManifestBytes) continue;
                using var reader = new StreamReader(stream);
                var buffer = new char[(int)stream.Length + 1];
                var length = reader.ReadBlock(buffer, 0, buffer.Length);
                if (reader.Peek() >= 0) continue;
                var content = new string(buffer, 0, length);
                var cwd = Path.GetDirectoryName(path)!;
                if (file.Kind == ".NET project")
                {
                    var xml = ParseXml(content);
                    var elements = xml.Descendants().ToArray();
                    if (elements.Any(e => e.Name.LocalName == "IsTestProject" && e.Value.Equals("true", StringComparison.OrdinalIgnoreCase))) continue;
                    var web = xml.Root?.Attribute("Sdk")?.Value.Split(';').Contains("Microsoft.NET.Sdk.Web") == true;
                    if (!web && !elements.Any(e => e.Name.LocalName == "OutputType" && e.Value.Trim() is "Exe" or "WinExe")) continue;
                    var frameworks = elements.Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                        .SelectMany(e => e.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray();
                    if (frameworks.Length > 1)
                        foreach (var framework in frameworks.Where(f => Regex.IsMatch(f, @"^net[\w.\-]+$")))
                            configurations.Add(new(projectId, $"{file.RelativePath} · {framework}", "dotnet", ["run", "--project", path, "--framework", framework], cwd));
                    else configurations.Add(new(projectId, file.RelativePath, "dotnet", ["run", "--project", path], cwd));
                }
                else if (file.Kind == "Node.js project")
                {
                    using var json = JsonDocument.Parse(content);
                    if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("scripts", out var scripts) || scripts.ValueKind != JsonValueKind.Object) continue;
                    var script = new[] { "start", "dev", "serve" }.FirstOrDefault(name => scripts.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()));
                    if (script is not null) configurations.Add(new(projectId, $"{file.RelativePath} · npm run {script}", "npm", ["run", script], cwd));
                }
                else if (file.Kind == "Maven project")
                {
                    // No repository wrappers or arbitrary exec goals: only explicit known plugin configuration.
                    var xml = ParseXml(content);
                    var plugins = xml.Descendants().Where(e => e.Name.LocalName == "plugin" && e.Ancestors().All(a => a.Name.LocalName != "pluginManagement")).ToArray();
                    var goal = plugins.Any(p => p.Elements().Any(e => e.Name.LocalName == "artifactId" && e.Value == "javafx-maven-plugin")) ? "javafx:run"
                        : plugins.Any(p => p.Elements().Any(e => e.Name.LocalName == "artifactId" && e.Value == "spring-boot-maven-plugin")) ? "spring-boot:run" : null;
                    if (goal is not null && !OperatingSystem.IsWindows()) configurations.Add(new(projectId, $"{file.RelativePath} · mvn {goal}", "mvn", [goal], cwd));
                }
                else if (!OperatingSystem.IsWindows() && Regex.IsMatch(content, "(?m)^\\s*(?:id\\s*\\(?\\s*['\"]application['\"]|application\\s*$|apply\\s+plugin:\\s*['\"]application['\"])"))
                    configurations.Add(new(projectId, $"{file.RelativePath} · gradle run", "gradle", ["run"], cwd));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or XmlException) { /* Invalid manifests never become guessed commands. */ }
        }
        return configurations;
    }

    private static XDocument ParseXml(string content)
    {
        using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }
}
