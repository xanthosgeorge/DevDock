using DevDock.Desktop.Models;
using DevDock.Desktop.ViewModels;

namespace DevDock.Desktop.Services;

public static class MockDataService
{
    public static ProjectViewModel[] CreateProjects() =>
    [
        new() { Name = "IronLog", Path = "~/Projects/IronLog", Branch = "main", GitStatus = "✓ Clean", LastActivity = "18m ago", Technologies = [".NET 10", "Angular", "PostgreSQL"], IsRunning = true,
            Services = [new("API", "localhost:5001", ServiceState.Running), new("Frontend", "localhost:4200", ServiceState.Running), new("PostgreSQL", "localhost:5432", ServiceState.Running)] },
        new() { Name = "Weather Widget", Path = "~/Projects/Weather-Widget", Branch = "main", GitStatus = "✓ Clean", LastActivity = "2h ago", Technologies = ["Java", "JavaFX", "SQLite"], IsRunning = true,
            Services = [new("Java application", "Local desktop app", ServiceState.Running), new("SQLite", "weather.db", ServiceState.Healthy)] },
        new() { Name = "iMaint Sandbox", Path = "~/Projects/iMaint-Dev", Branch = "feature/pm-plan", GitStatus = "2 modified files", LastActivity = "4h ago", Technologies = [".NET", "SQL Server"], RepositoryLabel = "Git", IsRunning = true,
            Services = [new("API", "localhost:8080", ServiceState.Running), new("Database", "localhost:1433", ServiceState.Running)] },
        new() { Name = "Example API", Path = "~/Projects/Example-API", Branch = "develop", GitStatus = "↑ 1 commit ahead", LastActivity = "1d ago", Technologies = ["ASP.NET Core", "Docker"],
            Services = [new("API", "localhost:7001", ServiceState.Stopped)] }
    ];
    public static DockerContainerViewModel[] CreateContainers() =>
    [new("ironlog-api", ":5001", "2h 14m", true), new("ironlog-postgres", ":5432", "2h 14m", true), new("weather-widget", "—", "Just started", false), new("imaint-sql", ":1433", "3h 2m", true), new("example-api", ":7001", "Just started", false)];
    public static PortViewModel[] CreatePorts() =>
    [new(4200, "node", "IronLog Frontend"), new(5001, "dotnet", "IronLog API"), new(5432, "postgres", "IronLog Database"), new(8080, "dotnet", "iMaint Sandbox"), new(1433, "sqlservr", "iMaint Database"), new(5173, "node", "Preview server")];
    public static ApiHealthViewModel[] CreateApis() =>
    [new("IronLog API", "http://localhost:5001", ServiceState.Healthy, 200, "21 ms"), new("iMaint API", "http://localhost:8080", ServiceState.Healthy, 200, "48 ms"), new("Example API", "http://localhost:7001", ServiceState.Warning, 503, "312 ms")];
}
