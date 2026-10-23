using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Services;

/// <summary>Bounded, read-only inspection of known project manifests. Never evaluates build files.</summary>
public sealed class ProjectDiscoveryService : IProjectDiscoveryService
{
    public const int MaxDepth = 3;
    public const int MaxDirectories = 128;
    public const int MaxEntriesPerDirectory = 512;
    public const int MaxProjectFiles = 256;
    public const int MaxManifestBytes = 1024 * 1024;
    public const int MaxTotalManifestBytes = 8 * MaxManifestBytes;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", "dist", "build", "packages", ".vs", ".idea",
        ".vscode", ".venv", "venv", "__pycache__", ".next", "target", "vendor", "coverage"
    };

    public Task<ProjectDiscoveryResult> DiscoverAsync(string projectPath) => Task.Run(() => Discover(projectPath));

    private static ProjectDiscoveryResult Discover(string projectPath)
    {
        var root = ProjectPath.Normalize(projectPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The selected project folder is no longer available.");
        var scan = new Scan(root);
        var directories = new Queue<(string Path, int Depth)>();
        directories.Enqueue((root, 0));
        var scheduled = 1;
        while (directories.TryDequeue(out var directory))
        {
            try
            {
                // Bound enumeration itself, rather than loading an entire directory first.
                var entries = Directory.EnumerateFileSystemEntries(directory.Path)
                    .Take(MaxEntriesPerDirectory + 1).ToArray();
                if (entries.Length > MaxEntriesPerDirectory) scan.Warn("Some entries were skipped because a folder is too large.");
                foreach (var path in entries.Take(MaxEntriesPerDirectory).Order(StringComparer.Ordinal))
                {
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        // Do not follow symlinks or junctions outside the selected tree, including linked manifests.
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            if (IgnoredDirectories.Contains(Path.GetFileName(path))) continue;
                            if (directory.Depth >= MaxDepth) { scan.Warn("Scan depth limit reached; deeper folders were not inspected."); continue; }
                            if (scheduled >= MaxDirectories) { scan.Warn("Folder scan limit reached; some subfolders were not inspected."); continue; }
                            directories.Enqueue((path, directory.Depth + 1));
                            scheduled++;
                        }
                        else scan.Inspect(path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        scan.Warn($"Could not read {Path.GetRelativePath(root, path)}.");
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (directory.Depth == 0) throw;
                scan.Warn($"Could not read folder {Path.GetRelativePath(root, directory.Path)}.");
            }
        }
        return scan.Result();
    }

    private sealed class Scan(string root)
    {
        private readonly HashSet<string> _technologies = new(StringComparer.Ordinal);
        private readonly List<DetectedProjectFile> _files = [];
        private readonly List<string> _warnings = [];
        private readonly Dictionary<string, string> _packageNames = new(StringComparer.Ordinal);
        private int _bytesRead;
        private bool _hasDocker;

        public void Warn(string message)
        {
            if (_warnings.Count < 20 && !_warnings.Contains(message)) _warnings.Add(message);
        }

        public void Inspect(string path)
        {
            var name = Path.GetFileName(path).ToLowerInvariant();
            var extension = Path.GetExtension(name);
            var kind = name switch
            {
                "package.json" => "Node.js project",
                "angular.json" => "Angular workspace",
                "pom.xml" => "Maven project",
                "build.gradle" or "build.gradle.kts" => "Gradle project",
                "pyproject.toml" or "requirements.txt" or "pipfile" or "setup.py" => "Python project",
                "go.mod" => "Go module",
                "cargo.toml" => "Rust crate",
                "dockerfile" => "Dockerfile",
                "docker-compose.yml" or "docker-compose.yaml" or "compose.yml" or "compose.yaml" => "Docker Compose",
                _ when extension is ".sln" or ".slnx" => ".NET solution",
                _ when extension is ".csproj" or ".fsproj" => ".NET project",
                _ when name.StartsWith("appsettings", StringComparison.Ordinal) && extension == ".json" => "Application configuration",
                _ => null
            };
            if (kind is null) return;
            if (_files.Count >= MaxProjectFiles) { Warn("Project file limit reached; additional manifests were skipped."); return; }
            var relative = Path.GetRelativePath(root, path);
            _files.Add(new(relative, kind));
            switch (kind)
            {
                case ".NET solution": _technologies.Add(".NET"); return;
                case "Angular workspace": _technologies.UnionWith(["Node.js", "Angular"]); return;
                case "Go module": _technologies.Add("Go"); return;
                case "Rust crate": _technologies.Add("Rust"); return;
                case "Dockerfile": _hasDocker = true; _technologies.Add("Docker"); return;
                case "Docker Compose": _hasDocker = true; _technologies.Add("Docker"); break;
                case ".NET project": _technologies.UnionWith([".NET", extension == ".fsproj" ? "F#" : "C#"]); break;
                case "Node.js project": _technologies.Add("Node.js"); break;
                case "Maven project": _technologies.UnionWith(["Java", "Maven"]); break;
                case "Gradle project": _technologies.UnionWith(["Java", "Gradle"]); break;
                case "Python project": _technologies.Add("Python"); break;
            }
            try
            {
                var content = ReadManifest(path);
                if (content is null) return;
                switch (kind)
                {
                    case ".NET project": InspectDotNet(content); break;
                    case "Node.js project": InspectPackage(content, relative); break;
                    case "Maven project": InspectMaven(content); break;
                    case "Gradle project":
                        var gradle = StripCommentLines(content);
                        if (gradle.Contains("org.openjfx", StringComparison.OrdinalIgnoreCase) || gradle.Contains("javafx.controls", StringComparison.OrdinalIgnoreCase)) _technologies.Add("JavaFX");
                        InspectDatabaseHints(gradle);
                        break;
                    case "Python project":
                        var python = StripCommentLines(content);
                        foreach (var framework in new[] { "Flask", "Django", "FastAPI" })
                            if (ContainsToken(python, framework)) _technologies.Add(framework);
                        InspectDatabaseHints(python);
                        break;
                    case "Docker Compose":
                        foreach (Match image in Regex.Matches(content, "(?im)^\\s*image:\\s*['\"]?([^\\s'\"#]+)")) InspectDatabaseHints(image.Groups[1].Value);
                        break;
                    case "Application configuration": InspectConfiguration(content); break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or XmlException)
            {
                Warn($"Could not inspect {relative}; the manifest is unreadable or invalid.");
            }
        }

        private string? ReadManifest(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MaxManifestBytes || stream.Length > MaxTotalManifestBytes - _bytesRead)
            {
                Warn("Manifest size limit reached; some file contents were not inspected.");
                return null;
            }
            _bytesRead += (int)stream.Length;
            using var reader = new StreamReader(stream);
            var buffer = new char[(int)Math.Min(stream.Length + 1, MaxManifestBytes + 1)];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            if (length > MaxManifestBytes || reader.Peek() >= 0) { Warn("An oversized or changing manifest was skipped."); return null; }
            return new string(buffer, 0, length);
        }

        private static XDocument ReadXml(string content)
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }

        private void InspectDotNet(string content)
        {
            var xml = ReadXml(content);
            if ((xml.Root?.DescendantsAndSelf() ?? []).Attributes().Any(a => a.Name.LocalName is "Sdk" or "Name" && a.Value.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)))
                _technologies.Add("ASP.NET Core");
            foreach (var framework in xml.Descendants().Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                         .SelectMany(e => e.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                var modern = Regex.Match(framework, @"^net(\d+)\.\d+(?:-|$)", RegexOptions.IgnoreCase);
                var legacy = Regex.Match(framework, @"^net(4\d{1,2})$", RegexOptions.IgnoreCase);
                if (modern.Success) _technologies.Add($".NET {modern.Groups[1].Value}");
                else if (legacy.Success) _technologies.Add(".NET Framework " + string.Join('.', legacy.Groups[1].Value.ToCharArray()));
                else if (Regex.IsMatch(framework, @"^netstandard\d+\.\d+$", RegexOptions.IgnoreCase)) _technologies.Add(".NET Standard " + framework[11..]);
            }
            foreach (var package in xml.Descendants().Where(e => e.Name.LocalName is "PackageReference" or "FrameworkReference"))
            {
                var name = package.Attribute("Include")?.Value ?? package.Attribute("Update")?.Value ?? "";
                if (name.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase)) _technologies.Add("ASP.NET Core");
                InspectDatabaseHints(name);
            }
        }

        private void InspectPackage(string content, string relative)
        {
            using var json = JsonDocument.Parse(content, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (json.RootElement.ValueKind != JsonValueKind.Object) { Warn($"{relative} is not a package object."); return; }
            if (json.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()))
                _packageNames[relative] = name.GetString()!;
            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies", "optionalDependencies" })
            {
                if (!json.RootElement.TryGetProperty(section, out var deps) || deps.ValueKind != JsonValueKind.Object) continue;
                foreach (var dependency in deps.EnumerateObject())
                {
                    var technology = dependency.Name switch
                    {
                        "@angular/core" => "Angular", "react" => "React", "vue" => "Vue", "next" => "Next.js", "typescript" => "TypeScript", _ => null
                    };
                    if (technology is not null) _technologies.Add(technology);
                    InspectDatabaseHints(dependency.Name);
                }
            }
        }

        private void InspectMaven(string content)
        {
            var xml = ReadXml(content);
            foreach (var dependency in xml.Descendants().Where(e => e.Name.LocalName == "dependency"))
            {
                var identifiers = string.Join(':', dependency.Elements().Where(e => e.Name.LocalName is "groupId" or "artifactId").Select(e => e.Value));
                if (identifiers.Contains("org.openjfx", StringComparison.OrdinalIgnoreCase)) _technologies.Add("JavaFX");
                InspectDatabaseHints(identifiers);
            }
        }

        private void InspectConfiguration(string content)
        {
            using var json = JsonDocument.Parse(content, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return;
            // Inspect provider identifiers only; never copy connection strings into the result.
            InspectProviders(json.RootElement);
        }

        private void InspectProviders(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    InspectDatabaseHints(property.Name);
                    if (property.Name.Equals("Provider", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("ProviderName", StringComparison.OrdinalIgnoreCase))
                        if (property.Value.ValueKind == JsonValueKind.String) InspectDatabaseHints(property.Value.GetString()!);
                    InspectProviders(property.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) InspectProviders(item);
        }

        private void InspectDatabaseHints(string text)
        {
            if (Regex.IsMatch(text, @"(?i)(?<![a-z])(?:npgsql|postgres(?:ql)?|psycopg[23]?|asyncpg|pg)(?![a-z])")) _technologies.Add("PostgreSQL");
            if (Regex.IsMatch(text, @"(?i)(?<![a-z])(?:sqlserver|mssql|sqlclient)(?![a-z])")) _technologies.Add("SQL Server");
            if (Regex.IsMatch(text, @"(?i)(?<![a-z])(?:sqlite[3]?|better-sqlite3)(?![a-z])")) _technologies.Add("SQLite");
            if (Regex.IsMatch(text, @"(?i)(?<![a-z])mysql(?:connector|client|[2])?(?![a-z])")) _technologies.Add("MySQL");
        }

        private static bool ContainsToken(string text, string token) => Regex.IsMatch(text, @"(?<![\w-])" + Regex.Escape(token) + @"(?![\w-])", RegexOptions.IgnoreCase);
        private static string StripCommentLines(string text) => string.Join('\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith('#') && !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        public ProjectDiscoveryResult Result()
        {
            if (_technologies.Any(t => t.StartsWith(".NET ", StringComparison.Ordinal))) _technologies.Remove(".NET");
            var solutions = _files.Where(f => f.Kind == ".NET solution" && Path.GetDirectoryName(f.RelativePath) == "").ToArray();
            var projects = _files.Where(f => f.Kind == ".NET project").ToArray();
            var rootProjects = projects.Where(f => Path.GetDirectoryName(f.RelativePath) == "").ToArray();
            var mainProject = rootProjects.Length == 1 ? rootProjects[0] : projects.Length == 1 ? projects[0] : null;
            var package = _packageNames.OrderBy(p => p.Key.Count(c => c == Path.DirectorySeparatorChar)).ThenBy(p => p.Key, StringComparer.Ordinal).FirstOrDefault();
            var solution = solutions.Length == 1 ? solutions[0] : null;
            var name = solution is not null ? Path.GetFileNameWithoutExtension(solution.RelativePath)
                : mainProject is not null ? Path.GetFileNameWithoutExtension(mainProject.RelativePath)
                : package.Value ?? new DirectoryInfo(root).Name;
            return new ProjectDiscoveryResult
            {
                Name = string.IsNullOrWhiteSpace(name) ? root : name,
                RootPath = root,
                Technologies = _technologies.Order(StringComparer.Ordinal).ToArray(),
                ProjectFiles = _files.OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToArray(),
                Warnings = _warnings.ToArray(),
                HasDocker = _hasDocker,
                HasGitRepository = Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git")),
                SolutionFile = solution?.RelativePath,
                IsSupported = _files.Any(f => f.Kind != "Application configuration")
            };
        }
    }
}
