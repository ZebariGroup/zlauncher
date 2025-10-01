using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace ZLauncher;

public partial class App : Application
{
    private readonly Lazy<LauncherConfigurationService> _configurationService = new(() => new LauncherConfigurationService());
    private readonly Lazy<StartMenuIndexService> _startMenuIndexService;

    public App()
    {
        _startMenuIndexService = new Lazy<StartMenuIndexService>(() => new StartMenuIndexService(_configurationService.Value));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = _configurationService.Value.LoadConfiguration();
        _startMenuIndexService.Value.BuildIndex();

        var mainWindow = new MainWindow();
        mainWindow.ViewModel.ApplyConfiguration(configuration);
        mainWindow.ViewModel.UpdateApplications(_startMenuIndexService.Value.Applications);
        mainWindow.Show();
    }
}

public sealed class LauncherConfigurationService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public LauncherConfigurationService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "ZLauncher");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _configPath = Path.Combine(directory, "launcher.json");

        if (!File.Exists(_configPath))
        {
            var defaultConfig = new LauncherConfiguration();
            var json = JsonSerializer.Serialize(defaultConfig, _serializerOptions);
            File.WriteAllText(_configPath, json);
        }
    }

    public LauncherConfiguration LoadConfiguration()
    {
        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<LauncherConfiguration>(json, _serializerOptions);
            return config ?? new LauncherConfiguration();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load configuration: {ex}");
            return new LauncherConfiguration();
        }
    }

    public void SaveConfiguration(LauncherConfiguration configuration)
    {
        var json = JsonSerializer.Serialize(configuration, _serializerOptions);
        File.WriteAllText(_configPath, json);
    }
}

public sealed class StartMenuIndexService
{
    private readonly LauncherConfigurationService _configurationService;
    private readonly List<ApplicationItem> _applications = new();

    public StartMenuIndexService(LauncherConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public IReadOnlyList<ApplicationItem> Applications => _applications;

    public void BuildIndex()
    {
        _applications.Clear();

        foreach (var link in EnumerateStartMenuEntries())
        {
            _applications.Add(link);
        }
    }

    private static IEnumerable<ApplicationItem> EnumerateStartMenuEntries()
    {
        var startMenuLocations = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var location in startMenuLocations)
        {
            if (Directory.Exists(location))
            {
                foreach (var shortcut in Directory.EnumerateFiles(location, "*.lnk", SearchOption.AllDirectories))
                {
                    var title = Path.GetFileNameWithoutExtension(shortcut);
                    yield return new ApplicationItem(title, "Shortcut", "Start Menu", new LaunchApplicationCommand(shortcut));
                }
            }
        }
    }
}

public sealed record LauncherConfiguration
{
    public List<PinnedActionConfiguration> Pinned { get; init; } = new();
    public List<MacroGroupConfiguration> MacroGroups { get; init; } = new();
    public List<RecentItemConfiguration> Recent { get; init; } = new();
    public List<AppFilterConfiguration> AppFilters { get; init; } = new();
    public List<TileGroupConfiguration> TileGroups { get; init; } = new();
}

public sealed record PinnedActionConfiguration(string Icon, string Title, string Description, string Command);

public sealed record MacroGroupConfiguration(string Name, List<WorkflowConfiguration> Actions);

public sealed record WorkflowConfiguration(string Title, string Description, string Command);

public sealed record RecentItemConfiguration(string Icon, string Title, string Subtitle, string Command);

public sealed record AppFilterConfiguration(string Name, string Predicate);

public sealed record TileGroupConfiguration(string Name, List<TileConfiguration> Tiles);

public sealed record TileConfiguration(string Icon, string Title, string Command);

public sealed class LaunchApplicationCommand : ICommand
{
    private readonly string _targetPath;

    public LaunchApplicationCommand(string targetPath)
    {
        _targetPath = targetPath;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => File.Exists(_targetPath);

    public void Execute(object? parameter)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _targetPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch {_targetPath}: {ex}");
        }
    }
}

