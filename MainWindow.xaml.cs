using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ZLauncher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public LauncherViewModel ViewModel { get; }

    private readonly HotKeyListener _hotKeyListener;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new LauncherViewModel();
        DataContext = ViewModel;

        _hotKeyListener = new HotKeyListener(this, new GlobalHotKey(ModifierKeys.Alt, Key.Space));
        _hotKeyListener.HotKeyTriggered += (_, _) => ToggleVisibility();
        _hotKeyListener.Register();
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            HideLauncher();
        }
        else
        {
            ShowLauncher();
        }
    }

    private void ShowLauncher()
    {
        Show();
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void HideLauncher()
    {
        Hide();
        ViewModel.SearchText = string.Empty;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnWindowDeactivated(object sender, EventArgs e)
    {
        HideLauncher();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideLauncher();
            e.Handled = true;
        }

        if (e.Key == Key.Down && !ViewModel.HasSearchText)
        {
            AppListView.Focus();
            AppListView.SelectedIndex = 0;
            e.Handled = true;
        }
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ViewModel.HasSearchText && ViewModel.SearchResults.FirstOrDefault() is SearchResult result)
            {
                result.LaunchCommand.Execute(null);
                HideLauncher();
                e.Handled = true;
                return;
            }
        }
    }

    private void OnLauncherButtonClick(object sender, RoutedEventArgs e)
    {
        HideLauncher();
    }

    private void OnApplicationItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AppListView.SelectedItem is ApplicationItem app)
        {
            app.LaunchCommand.Execute(null);
            HideLauncher();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotKeyListener.Dispose();
        base.OnClosed(e);
    }
}

public sealed class LauncherViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private AppFilter? _selectedAppFilter;
    private LauncherConfiguration? _configuration;

    public ObservableCollection<PinnedAction> PinnedActions { get; } = new();
    public ObservableCollection<MacroGroup> MacroGroups { get; } = new();
    public ObservableCollection<RecentItem> RecentItems { get; } = new();
    public ObservableCollection<AppFilter> AppFilters { get; } = new();
    public ObservableCollection<ApplicationItem> Applications { get; } = new();
    public ObservableCollection<ApplicationItem> FilteredApplications { get; } = new();
    public ObservableCollection<TileGroup> TileGroups { get; } = new();
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    public LauncherViewModel()
    {
        // Initialization will be replaced by bootstrapping services.
        AppFilters.Add(new AppFilter("All"));
        SelectedAppFilter = AppFilters[0];
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public string SearchResultsHeader => HasSearchText ? "Search Results" : "Recent";

    public void ApplyConfiguration(LauncherConfiguration configuration)
    {
        _configuration = configuration;

        PinnedActions.Clear();
        foreach (var pinned in configuration.Pinned)
        {
            PinnedActions.Add(new PinnedAction(pinned.Icon, pinned.Title, pinned.Description, CommandFactory.Create(pinned.Command)));
        }

        MacroGroups.Clear();
        foreach (var group in configuration.MacroGroups)
        {
            var actions = new ObservableCollection<WorkflowAction>();
            foreach (var action in group.Actions)
            {
                actions.Add(new WorkflowAction(action.Title, action.Description, CommandFactory.Create(action.Command)));
            }

            MacroGroups.Add(new MacroGroup(group.Name, actions));
        }

        RecentItems.Clear();
        foreach (var recent in configuration.Recent)
        {
            RecentItems.Add(new RecentItem(recent.Icon, recent.Title, recent.Subtitle, CommandFactory.Create(recent.Command)));
        }

        AppFilters.Clear();
        AppFilters.Add(new AppFilter("All"));

        foreach (var filter in configuration.AppFilters)
        {
            if (FilterParser.TryParse(filter.Predicate, out var predicate))
            {
                AppFilters.Add(new AppFilter(filter.Name, predicate));
            }
        }

        TileGroups.Clear();
        foreach (var group in configuration.TileGroups)
        {
            var tiles = new ObservableCollection<TileItem>();
            foreach (var tile in group.Tiles)
            {
                tiles.Add(new TileItem(tile.Icon, tile.Title, CommandFactory.Create(tile.Command)));
            }

            TileGroups.Add(new TileGroup(group.Name, tiles));
        }

        SelectedAppFilter = AppFilters.FirstOrDefault();
    }

    public void UpdateApplications(IEnumerable<ApplicationItem> applications)
    {
        Applications.Clear();
        foreach (var application in applications)
        {
            Applications.Add(application);
        }

        UpdateFilteredApplications();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                UpdateFilteredApplications();
                UpdateSearchResults();
                OnPropertyChanged(nameof(HasSearchText));
                OnPropertyChanged(nameof(SearchResultsHeader));
            }
        }
    }

    public AppFilter? SelectedAppFilter
    {
        get => _selectedAppFilter;
        set
        {
            if (SetField(ref _selectedAppFilter, value))
            {
                UpdateFilteredApplications();
            }
        }
    }

    private void UpdateSearchResults()
    {
        SearchResults.Clear();

        if (!HasSearchText)
        {
            return;
        }

        foreach (var app in Applications)
        {
            if (app.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                SearchResults.Add(new SearchResult(app.Icon, app.Title, app.Source, app.Category, app.LaunchCommand));
            }
        }

        foreach (var pinned in PinnedActions)
        {
            if (pinned.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                SearchResults.Add(new SearchResult(pinned.Icon, pinned.Title, pinned.Description, "Pinned", pinned.LaunchCommand));
            }
        }
    }

    private void UpdateFilteredApplications()
    {
        FilteredApplications.Clear();
        foreach (var app in Applications)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) && !app.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (SelectedAppFilter is not null && !SelectedAppFilter.AppliesTo(app))
            {
                continue;
            }

            FilteredApplications.Add(app);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public static class CommandFactory
{
    public static ICommand Create(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new NoOpCommand();
        }

        if (command.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return new ShellCommand(command[6..].Trim());
        }

        if (command.StartsWith("workflow:", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowCommandParser.Parse(command[9..].Trim());
        }

        return new LaunchApplicationCommand(command);
    }
}

public sealed class NoOpCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        // No operation.
    }
}

public sealed class ShellCommand : ICommand
{
    private readonly string _command;

    public ShellCommand(string command)
    {
        _command = command;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{_command}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to execute shell command '{_command}': {ex}");
        }
    }
}

public static class WorkflowCommandParser
{
    public static ICommand Parse(string workflow)
    {
        var commands = workflow.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (commands.Length == 0)
        {
            return new NoOpCommand();
        }

        var launchers = new List<ICommand>();
        foreach (var command in commands)
        {
            launchers.Add(CommandFactory.Create(command));
        }

        return new CompositeCommand(launchers);
    }
}

public sealed class CompositeCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _commands;

    public CompositeCommand(IReadOnlyList<ICommand> commands)
    {
        _commands = commands;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _commands.All(c => c.CanExecute(parameter));

    public void Execute(object? parameter)
    {
        foreach (var command in _commands)
        {
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }
    }
}

public static class FilterParser
{
    public static bool TryParse(string expression, out Func<ApplicationItem, bool> predicate)
    {
        predicate = _ => true;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        // Simple syntax: property:value
        if (expression.Contains(':'))
        {
            var parts = expression.Split(':', 2);
            var property = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim();

            predicate = property switch
            {
                "source" => app => string.Equals(app.Source, value, StringComparison.OrdinalIgnoreCase),
                "category" => app => string.Equals(app.Category, value, StringComparison.OrdinalIgnoreCase),
                _ => app => app.Title.Contains(value, StringComparison.OrdinalIgnoreCase)
            };

            return true;
        }

        predicate = app => app.Title.Contains(expression, StringComparison.OrdinalIgnoreCase);
        return true;
    }
}

public sealed record PinnedAction(string Icon, string Title, string Description, ICommand LaunchCommand);

public sealed record MacroGroup(string Name, ObservableCollection<WorkflowAction> Actions);

public sealed record WorkflowAction(string Title, string Description, ICommand LaunchCommand);

public sealed record RecentItem(string Icon, string Title, string Subtitle, ICommand LaunchCommand);

public sealed record AppFilter(string Name, Func<ApplicationItem, bool>? Predicate = null)
{
    public bool AppliesTo(ApplicationItem app) => Predicate?.Invoke(app) ?? true;
}

public sealed record ApplicationItem(string Title, string Category, string Source, ICommand LaunchCommand, string Icon = "");

public sealed record TileGroup(string Name, ObservableCollection<TileItem> Tiles);

public sealed record TileItem(object Icon, string Title, ICommand LaunchCommand);

public sealed record SearchResult(string Icon, string Title, string Subtitle, string Category, ICommand LaunchCommand);