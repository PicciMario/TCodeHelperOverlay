using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TCodeLaunchpad.App.Services;
using TCodeLaunchpad.App.ViewModels;
using TCodeLaunchpad.Core.Data;
using TCodeLaunchpad.Core.Search;
using TCodeLaunchpad.Core.Services;

namespace TCodeLaunchpad.App;

public partial class MainWindow : Window
{
    private const int MaxVisibleResults = 15;

    private readonly ObservableCollection<ResultRowViewModel> _rows = new();
    private readonly TransactionSearchService _searchService;
    private GlobalHotkeyService? _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly DispatcherTimer _searchDebounce;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        TryEnableBlur();

        ResultsList.ItemsSource = _rows;

        var searchOptions = new SearchOptions();
        var dataPath = ResolveDataPath();
        _searchService = new TransactionSearchService(new JsonTransactionRepository(), new WeightedSearchEngine(searchOptions), dataPath);
        _searchService.Reload();

        _searchDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(140)
        };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            ApplySearch(SearchBox.Text);
        };

        SourceInitialized += (_, _) =>
        {
            _hotkeyService = new GlobalHotkeyService(this);
            _hotkeyService.HotkeyPressed += (_, _) => Dispatcher.Invoke(ToggleLauncher);
            _hotkeyService.TryRegisterCtrlSpace(out _);
        };

        _trayIconService = new TrayIconService(
            () => Dispatcher.Invoke(ShowLauncher),
            () => Dispatcher.Invoke(HideLauncher),
            () => Dispatcher.Invoke(ReloadData),
            () => Dispatcher.Invoke(ExitApplication));

        Closing += MainWindow_Closing;
    }

    public void HideLauncher()
    {
        Hide();
    }

    public void ShowLauncherFromActivation()
    {
        ShowLauncher();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            HideLauncher();
            return;
        }

        _hotkeyService?.Dispose();
        _trayIconService.Dispose();
    }

    private void ShowLauncher()
    {
        Show();
        Activate();
        Focus();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void ToggleLauncher()
    {
        if (IsVisible)
        {
            HideLauncher();
            return;
        }

        ShowLauncher();
    }

    private void ReloadData()
    {
        _searchService.Reload();
        ApplySearch(SearchBox.Text);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            _searchDebounce.Stop();
            ApplySearch(SearchBox.Text);
            EnsureListFocus();
            MoveSelection(1);
            FocusSelectedResultItem();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            _searchDebounce.Stop();
            ApplySearch(SearchBox.Text);
            EnsureListFocus();
            MoveSelection(-1);
            FocusSelectedResultItem();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CopyCurrentSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            HideLauncher();
            e.Handled = true;
        }
    }

    private void ResultsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            FocusSearchBoxForReplace();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            MoveSelection(1);
            FocusSelectedResultItem();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            MoveSelection(-1);
            FocusSelectedResultItem();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CopyCurrentSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            HideLauncher();
            e.Handled = true;
        }
    }

    private void DetailsTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            FocusSearchBoxForReplace();
            e.Handled = true;
        }
    }

    private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchBox.SelectAll();
    }

    private void ResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateDetailsPanel();
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _rows.Clear();
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
            ResultsHost.Visibility = Visibility.Collapsed;
            return;
        }

        ResultsHost.Visibility = Visibility.Visible;

        var results = _searchService.Search(query)
            .Take(MaxVisibleResults)
            .Select(result => ResultRowViewModel.FromSearchResult(result, query))
            .ToList();

        _rows.Clear();
        foreach (var row in results)
        {
            _rows.Add(row);
        }

        if (_rows.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            UpdateDetailsPanel();
        }
        else
        {
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var current = ResultsList.SelectedIndex;
        if (current < 0)
        {
            ResultsList.SelectedIndex = delta > 0 ? 0 : _rows.Count - 1;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            return;
        }

        var next = Math.Clamp(current + delta, 0, _rows.Count - 1);
        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void FocusSelectedResultItem()
    {
        if (_rows.Count == 0 || ResultsList.SelectedIndex < 0)
        {
            return;
        }

        ResultsList.Focus();
        ResultsList.UpdateLayout();

        if (ResultsList.ItemContainerGenerator.ContainerFromIndex(ResultsList.SelectedIndex) is System.Windows.Controls.ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
        }
    }

    private void EnsureListFocus()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        ResultsList.Focus();
        Keyboard.Focus(ResultsList);
    }

    private void FocusSearchBoxForReplace()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    private void CopyCurrentSelection()
    {
        var row = ResultsList.SelectedItem as ResultRowViewModel;
        if (row is null && _rows.Count > 0)
        {
            row = _rows[0];
        }

        if (row is null)
        {
            return;
        }

        var clipboardValue = $"/n{row.Code}";
        System.Windows.Clipboard.SetText(clipboardValue);

        HideLauncher();
        _trayIconService.ShowToast($"Code {row.Code} copied in the clipboard");
    }

    private void ConfigureWindow()
    {
        Topmost = true;
        WindowState = WindowState.Normal;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        var searchTop = Math.Max(24, Height / 3);
        SearchHost.Margin = new Thickness(24, searchTop, 24, 24);

        const double searchHeight = 42;
        const double searchHostPaddingVertical = 40;
        const double verticalGap = 14;
        var resultsTop = searchTop + searchHeight + searchHostPaddingVertical + verticalGap;
        ResultsHost.Margin = new Thickness(24, resultsTop, 24, 24);
    }

    private void UpdateDetailsPanel()
    {
        if (ResultsList.SelectedItem is not ResultRowViewModel selected)
        {
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
            return;
        }

        DetailsTextBox.Text =
            $"Code: {selected.Code}{Environment.NewLine}" +
            $"Module: {selected.Module}{Environment.NewLine}" +
            $"Keywords: {selected.Keywords}{Environment.NewLine}{Environment.NewLine}" +
            selected.LongDescription;

        DebugTextBox.Text = $"Debug ({selected.FilterText}): {selected.ScoreDebugText}";
    }

    private void TryEnableBlur()
    {
        BlurService.TryEnableBlur(this);
    }

    private static string ResolveDataPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "data.json"),
            Path.Combine(AppContext.BaseDirectory, "data.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data.json")
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException("Unable to locate data.json. Place it next to the executable or in the workspace root.");
    }
}
