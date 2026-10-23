# DevDock

**Your local development environment at a glance.**

DevDock is a cross-platform desktop control center for local software development environments. It brings projects, services and environment status into one focused workspace, so you can see what is running and where your attention is needed.

Built for developers who work across multiple repositories, stacks and local services.

## Current status

**Local Project Discovery — the first real feature after v0.1.**

The desktop experience now supports importing a local project through a native folder picker, bounded asynchronous manifest discovery and a confirmation dialog. Imported projects display their detected technologies and Git repository presence. Duplicate root paths are rejected.

Four **DEMO** projects remain alongside imported **LOCAL** projects. Containers, ports, API responses, logs and system statistics still use mock data. Restarting the app clears imported projects and restores the sample environment. Git status and other integrations are planned for later steps.

## Inside the prototype

- **Project workspace** — four sample projects with technology badges, Git metadata, service endpoints and running states.
- **Environment overview** — compact counts for projects, running projects, containers, open ports and monitored APIs.
- **Docker panel** — sample containers with mock Start/Stop controls.
- **Ports panel** — port numbers, process names and associated projects.
- **API health panel** — sample health states, HTTP status codes and response times.
- **System overview** — simulated CPU, memory and disk usage.
- **Local project import** — native folder selection, asynchronous discovery, technology previews, scan warnings and duplicate detection.
- **Desktop interactions** — sidebar navigation, exclusive project selection and refresh feedback.
- **Dark interface** — graphite surfaces, restrained blue accents, compact status indicators and independently scrolling dashboard columns.

The sample workspace includes IronLog, Weather Widget, iMaint Sandbox and Example API. Demo project and container controls update independently; health results remain fixed sample responses. Imported projects do not show fabricated branches, service states or Start/Stop actions.

## Tech stack

| Layer | Technology |
| --- | --- |
| Language | C# |
| Runtime | .NET 10 |
| Desktop UI | Avalonia UI 12.1.4 |
| Views and styling | XAML with centralized resources |
| Application pattern | MVVM |
| Observable state and commands | CommunityToolkit.Mvvm 8.4.2 |
| Data | Local manifest discovery and in-memory demo data |

Windows and macOS are the target platforms. The prototype has been built and launched on Windows; macOS validation and platform packaging are still pending.

## Getting started

### Prerequisites

- .NET 10 SDK
- Windows or macOS
- Optional: Visual Studio or JetBrains Rider for editing and debugging

### Clone and run

```sh
git clone https://github.com/xanthosgeorge/DevDock.git
cd DevDock
dotnet restore DevDock.sln
dotnet build DevDock.sln
dotnet run --project src/DevDock.Desktop/DevDock.Desktop.csproj
```

This repository is private, so cloning requires GitHub access.

You can also open `DevDock.sln` in your IDE and select `DevDock.Desktop` as the startup project.

The initial window size is **1400 × 900**, with a minimum of **1100 × 700**. At smaller sizes, service tiles and action buttons wrap while the project and monitoring columns scroll independently.

## Project structure

```text
DevDock.sln
src/
  DevDock.Desktop/
    App.axaml
    Program.cs
    Controls/       Reusable project cards, status indicators and technology badges
    Models/         Discovery results and service-state types
    Services/       Bounded discovery, UI import boundary and mock data
    Styles/         Colors, typography and control styles
    ViewModels/     Observable state, navigation and commands
    Views/          Main window and dashboard layout
tests/
  DevDock.Desktop.Tests/   Discovery and import-flow tests using temporary fixtures
```

The application deliberately stays within one desktop project, with a separate test project. View models own application state and interactions; views handle presentation. `DashboardViewModel` coordinates `IProjectDiscoveryService` and `IProjectImportInteraction`. The filesystem scanner has no Avalonia dependency. The desktop interaction adapter uses Avalonia's storage APIs and a styled modal window. Code-behind is limited to initialization, modal close results and pointer-based card selection.

Reusable controls:

| Control | Purpose |
| --- | --- |
| `ProjectCard` | Project identity, stack, Git metadata, services and actions |
| `StatusIndicator` | Compact dot and optional text for running, healthy, warning, stopped, error and unknown states |
| `TechnologyBadge` | Subtle, consistent technology labels |

## Adding a local project

1. Click **+ Add Project** and choose a local project or repository folder.
2. Wait for **Scanning project…**. Import stays disabled while the operation is active.
3. Review the detected name, root path, technology badges, project files and any scan warnings.
4. Click **Add Project** to confirm, or **Cancel** to leave the workspace unchanged.
5. The new **LOCAL** card appears first in Projects and is selected automatically.

An empty or unsupported folder shows an explanation. Selecting the same normalized root path again shows **This project is already in DevDock.** No project is added until confirmation.

### Supported detection

| Files | Detected technologies |
| --- | --- |
| `.sln`, `.slnx`, `.csproj`, `.fsproj` | .NET, C# / F#, literal target frameworks, ASP.NET Core when explicitly configured |
| `pom.xml`, `build.gradle`, `build.gradle.kts` | Java, Maven / Gradle, JavaFX when explicit dependencies or modules are found |
| `package.json`, `angular.json` | Node.js, Angular, React, Vue, Next.js, TypeScript |
| `pyproject.toml`, `requirements.txt`, `Pipfile`, `setup.py` | Python, Flask, Django, FastAPI hints |
| `go.mod`, `Cargo.toml` | Go, Rust |
| `Dockerfile`, `docker-compose.yml`, `docker-compose.yaml`, `compose.yml`, `compose.yaml` | Docker configuration presence |
| Known dependency identifiers, Compose images and explicit JSON provider metadata | PostgreSQL, SQL Server, SQLite, MySQL hints |

Only the presence of a root `.git` directory or file is reported. Its contents, branch, changes, commits and remotes are not inspected.

### Discovery limits and safety

Discovery runs on a background task. It reads recognized manifests without evaluating project files, executing code, installing packages, invoking tools or connecting to services. Project names prefer an unambiguous root solution, a main project file, a package name and finally the selected directory name.

- Scans the root and up to **3 subdirectory levels** using breadth-first traversal.
- Limits each import to **128 directories**, **512 entries per directory** and **256 recognized files**.
- Reads at most **1 MiB per manifest** and **8 MiB of manifest contents** per scan.
- Skips child symlinks/junctions and symbolic-link manifests.
- Ignores `bin`, `obj`, `node_modules`, `.git`, `dist`, `build`, `packages`, `.vs`, `.idea`, `.vscode`, `.venv`, `venv`, `__pycache__`, `.next`, `target`, `vendor` and `coverage`.
- Reports unreadable or malformed manifests and scan limits in the preview. A recognized manifest can still identify a project even when its contents cannot be inspected.
- Rejects external XML entities. Connection strings and manifest contents are never included in discovery results.

Windows duplicate comparison ignores casing. Unix paths, including macOS paths, retain case sensitivity to support case-sensitive volumes. Trailing separators and relative path segments are normalized. Symlink aliases and differently cased paths on case-insensitive macOS volumes are not resolved to filesystem identities.

This is intentionally heuristic discovery. Deep monorepos may need a more specific selected folder. MSBuild imports and variables, transitive dependencies, arbitrary configuration formats and dynamically computed build settings are not evaluated. Database badges are hints rather than connection checks.

## Tests

```sh
dotnet test DevDock.sln
```

The test suite creates disposable project fixtures and covers detection, malformed/oversized manifests, ignored folders, naming, import confirmation/cancellation, duplicate paths and loading/error states. It does not require projects, language runtimes or local services on the developer machine.

For manual testing, import this repository to detect the root DevDock solution and nested .NET projects. Cancel a preview to confirm no card is added, reimport a confirmed root to check duplicates, and select an empty folder to check the unsupported-folder message. Resize the app to its minimum size and review the preview's scrolling content.

## Current boundaries

DevDock does not connect to Git, Docker, databases or local services. It does not scan processes or ports, send health requests, collect system metrics, launch IDEs or terminals, watch the filesystem or persist workspace state. Folder selection uses Avalonia's cross-platform storage provider. macOS execution remains unverified in this Windows development environment.

## Vision

A developer should be able to open DevDock and immediately understand:

- Which projects are active and which branches they use.
- Which services and containers are running.
- Which processes occupy local ports.
- Which APIs are healthy and where failures appear.
- Where to open a project's tools, logs and repository.

Future capabilities include automatic project discovery, Git status, Docker and process monitoring, open-port detection, API health checks, service orchestration, logs, system metrics, and IDE/terminal shortcuts.

DevDock complements the developer's existing tools by bringing local environment context into one place.

## Roadmap

| Version | Focus |
| --- | --- |
| **v0.1** | UI prototype with realistic mock data |
| **v0.2** | Project discovery (implemented); Git integration (planned next) |
| **v0.3** | Processes, ports and IDE/terminal launching |
| **v0.4** | Docker integration |
| **v0.5** | `.devdock.yml` and service orchestration |
| **v0.6** | API health monitoring, logs and system metrics |
| **v1.0** | Persistence, settings, Windows/macOS packaging and polish |

The current implementation stops at local project discovery. The remaining roadmap entries describe planned work, not available integrations.
