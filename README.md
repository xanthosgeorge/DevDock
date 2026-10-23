# DevDock

**Your local development environment at a glance.**

DevDock is a cross-platform desktop control center for local software development environments. It brings projects, services and environment status into one focused workspace, so you can see what is running and where your attention is needed.

Built for developers who work across multiple repositories, stacks and local services.

## Current status

**v0.1 — UI Prototype**

The first version establishes the desktop experience using realistic mock data. Projects, containers, ports, API responses, logs and system statistics are simulated. Actions update in-memory state or display a preview message; restarting the app restores the sample environment.

Real integrations will be introduced incrementally in later versions.

## Inside the prototype

- **Project workspace** — four sample projects with technology badges, Git metadata, service endpoints and running states.
- **Environment overview** — compact counts for projects, running projects, containers, open ports and monitored APIs.
- **Docker panel** — sample containers with mock Start/Stop controls.
- **Ports panel** — port numbers, process names and associated projects.
- **API health panel** — sample health states, HTTP status codes and response times.
- **System overview** — simulated CPU, memory and disk usage.
- **Desktop interactions** — sidebar navigation, exclusive project selection, refresh feedback and an Add Project preview dialog.
- **Dark interface** — graphite surfaces, restrained blue accents, compact status indicators and independently scrolling dashboard columns.

The sample workspace includes IronLog, Weather Widget, iMaint Sandbox and Example API. Project and container controls update independently; health results remain fixed sample responses.

## Tech stack

| Layer | Technology |
| --- | --- |
| Language | C# |
| Runtime | .NET 10 |
| Desktop UI | Avalonia UI 12.1.4 |
| Views and styling | XAML with centralized resources |
| Application pattern | MVVM |
| Observable state and commands | CommunityToolkit.Mvvm 8.4.2 |
| Data | In-memory mock data |

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
    Models/         Shared mock-state types
    Services/       MockDataService
    Styles/         Colors, typography and control styles
    ViewModels/     Observable state, navigation and commands
    Views/          Main window and dashboard layout
```

The architecture deliberately stays within one desktop project. View models own application state and interactions; views handle presentation. Code-behind is limited to initialization and pointer-based card selection.

Reusable controls:

| Control | Purpose |
| --- | --- |
| `ProjectCard` | Project identity, stack, Git metadata, services and actions |
| `StatusIndicator` | Compact dot and optional text for running, healthy, warning, stopped, error and unknown states |
| `TechnologyBadge` | Subtle, consistent technology labels |

## Prototype boundaries

DevDock v0.1 does not connect to Git, Docker, databases or local services. It does not scan processes or ports, send health requests, collect system metrics, launch IDEs or terminals, watch the filesystem or save workspace state.

The prototype is intended to make the product's layout and interactions concrete before introducing platform services and persistence.

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
| **v0.2** | Project discovery and Git integration |
| **v0.3** | Processes, ports and IDE/terminal launching |
| **v0.4** | Docker integration |
| **v0.5** | `.devdock.yml` and service orchestration |
| **v0.6** | API health monitoring, logs and system metrics |
| **v1.0** | Persistence, settings, Windows/macOS packaging and polish |

The current implementation stops at v0.1. Roadmap entries describe planned work, not available integrations.
