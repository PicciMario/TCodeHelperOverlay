using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;
using TCodeLaunchpad.App.Services;
using TCodeLaunchpad.App.ViewModels;
using TCodeLaunchpad.Core.Data;
using TCodeLaunchpad.Core.Search;
using TCodeLaunchpad.Core.Services;

namespace TCodeLaunchpad.App;

public partial class MainWindow : Window
{
    private const int MaxVisibleResults = 15;
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;

    private static readonly System.Windows.Media.Brush SearchNormalForeground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3AEFFAE")!);
    private static readonly System.Windows.Media.Brush BoPrefixBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF80CCFF")!);
    private static readonly System.Windows.Media.Brush ModulePrefixBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFD080")!);

    private readonly ObservableCollection<ResultRowViewModel> _rows = new();
    private readonly ObservableCollection<DetailFacetOption> _detailFacetOptions = new();
    private readonly DataCacheService _dataCacheService;
    private readonly TransactionSearchService _searchService;
    private GlobalHotkeyService? _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly DispatcherTimer _searchDebounce;
    private bool _isExiting;
    private bool _isRefreshingCache;
    private bool _suppressPrefixSuggestions;
    private bool _programmaticSearchUpdate;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        TryEnableBlur();

        ResultsList.ItemsSource = _rows;
        DetailsFacetList.ItemsSource = _detailFacetOptions;

        var searchOptions = new SearchOptions();
        _dataCacheService = new DataCacheService();
        _searchService = new TransactionSearchService(new JsonTransactionRepository(), new WeightedSearchEngine(searchOptions), _dataCacheService.CacheFilePath);
        CachePathRun.Text = _dataCacheService.CacheFilePath;
        CacheAgeRun.Text = "loading...";

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
            _hotkeyService.HotkeyPressed += (_, args) => Dispatcher.Invoke(() => ToggleLauncher(args.CursorX, args.CursorY));
            _hotkeyService.TryRegisterCtrlSpace(out _);
        };

        _trayIconService = new TrayIconService(
            () => Dispatcher.Invoke(ShowLauncher),
            () => Dispatcher.Invoke(HideLauncher),
            () => Dispatcher.Invoke(ReloadData),
            () => Dispatcher.Invoke(ExitApplication));

        Loaded += async (_, _) => await RefreshCacheIfNeededAsync(forceRefresh: false);
        Closing += MainWindow_Closing;
    }

    public void HideLauncher()
    {
        Hide();
    }

    public void ShowLauncherFromActivation()
    {
        ShowLauncherAtCursor(WinForms.Control.MousePosition);
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
        ShowLauncherAtCursor(WinForms.Control.MousePosition);
    }

    private void ShowLauncherAtCursor(DrawingPoint cursorPosition)
    {
        ConfigureWindow(cursorPosition);
        Show();
        Activate();
        Focus();
        SearchBox.Focus();
        SearchBox.SelectAll();
        _ = RefreshCacheIfNeededAsync(forceRefresh: false);
    }

    private void ToggleLauncher(int cursorX, int cursorY)
    {
        if (IsVisible)
        {
            HideLauncher();
            return;
        }

        ShowLauncherAtCursor(new DrawingPoint(cursorX, cursorY));
    }

    private void ReloadData()
    {
        _ = RefreshCacheIfNeededAsync(forceRefresh: true);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_programmaticSearchUpdate)
        {
            _suppressPrefixSuggestions = false;
        }

        UpdateSearchHighlight(SearchBox.Text);
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void UpdateSearchHighlight(string text)
    {
        if (TryGetPrefixForHighlight(text, out var prefix, out var value, out var filterType))
        {
            SearchBox.Foreground = System.Windows.Media.Brushes.Transparent;
            SearchBoxHighlight.Visibility = Visibility.Visible;
            SearchBoxHighlight.Inlines.Clear();
            var prefixBrush = filterType == "bo" ? BoPrefixBrush : ModulePrefixBrush;
            SearchBoxHighlight.Inlines.Add(new Run(prefix) { Foreground = prefixBrush });
            SearchBoxHighlight.Inlines.Add(new Run(value) { Foreground = SearchNormalForeground });
        }
        else
        {
            SearchBox.Foreground = SearchNormalForeground;
            SearchBoxHighlight.Visibility = Visibility.Collapsed;
            SearchBoxHighlight.Inlines.Clear();
        }
    }

    private static bool TryGetPrefixForHighlight(string text, out string prefix, out string value, out string filterType)
    {
        prefix = value = filterType = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.StartsWith("bo:", StringComparison.OrdinalIgnoreCase))
        {
            prefix = text[..3];
            value = text[3..];
            filterType = "bo";
            return true;
        }

        if (text.StartsWith("module:", StringComparison.OrdinalIgnoreCase))
        {
            prefix = text[..7];
            value = text[7..];
            filterType = "module";
            return true;
        }

        return false;
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

        if (e.Key == Key.Right)
        {
            FocusDetailsFacetList();
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
            return;
        }

        if (e.Key == Key.Right)
        {
            FocusDetailsFacetList();
            e.Handled = true;
        }
    }

    private void DetailsFacetList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            FocusSearchBoxForReplace();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ApplyRelatedFilterFromSelectedFacet();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            MoveFacetSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            MoveFacetSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left)
        {
            EnsureListFocus();
            FocusSelectedResultItem();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            HideLauncher();
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
            _detailFacetOptions.Clear();
            DetailsFacetList.Visibility = Visibility.Collapsed;
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
            ResultsHost.Visibility = Visibility.Collapsed;
            return;
        }

        ResultsHost.Visibility = Visibility.Visible;

        if (!_suppressPrefixSuggestions && TryParseBoSuggestionQuery(query, out var boPrefix))
        {
            ShowBoSuggestions(boPrefix);
            return;
        }

        if (!_suppressPrefixSuggestions && TryParseModuleSuggestionQuery(query, out var modulePrefix))
        {
            ShowModuleSuggestions(modulePrefix);
            return;
        }

        var results = ResolveResults(query)
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
            _detailFacetOptions.Clear();
            DetailsFacetList.Visibility = Visibility.Collapsed;
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
        }
    }

    private static bool TryParseBoSuggestionQuery(string query, out string boPrefix)
    {
        boPrefix = string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        var trimmed = query.Trim();
        if (!trimmed.StartsWith("bo:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        boPrefix = trimmed[3..].Trim();
        return true;
    }

    private static bool TryParseModuleSuggestionQuery(string query, out string modulePrefix)
    {
        modulePrefix = string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        var trimmed = query.Trim();
        if (!trimmed.StartsWith("module:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        modulePrefix = trimmed[7..].Trim();
        return true;
    }

    private void ShowBoSuggestions(string prefix)
    {
        var suggestions = _searchService.GetBusinessObjectSuggestions(prefix)
            .Take(MaxVisibleResults)
            .Select(s => ResultRowViewModel.FromBoSuggestion(s.Code, s.Name, s.TransactionCount, $"bo:{s.Code}"))
            .ToList();

        _rows.Clear();
        _detailFacetOptions.Clear();
        DetailsFacetList.Visibility = Visibility.Collapsed;

        foreach (var row in suggestions)
        {
            _rows.Add(row);
        }

        if (_rows.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        }

        DetailsTextBox.Text = string.Empty;
        DebugTextBox.Text = string.Empty;
    }

    private void ShowModuleSuggestions(string prefix)
    {
        var suggestions = _searchService.GetModuleSuggestions(prefix)
            .Take(MaxVisibleResults)
            .Select(s => ResultRowViewModel.FromModuleSuggestion(s.Code, s.Name, s.TransactionCount, $"module:{s.Code}"))
            .ToList();

        _rows.Clear();
        _detailFacetOptions.Clear();
        DetailsFacetList.Visibility = Visibility.Collapsed;

        foreach (var row in suggestions)
        {
            _rows.Add(row);
        }

        if (_rows.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        }

        DetailsTextBox.Text = string.Empty;
        DebugTextBox.Text = string.Empty;
    }

    private IReadOnlyList<SearchResult> ResolveResults(string query)
    {
        if (!TryParseRelatedFilterQuery(query, out var filterType, out var filterValue))
        {
            return _searchService.Search(query);
        }

        return filterType switch
        {
            "module" => _searchService.SearchByModule(filterValue),
            "bo" => _searchService.SearchByBusinessObjectCode(filterValue),
            _ => _searchService.Search(query)
        };
    }

    private static bool TryParseRelatedFilterQuery(string query, out string filterType, out string filterValue)
    {
        filterType = string.Empty;
        filterValue = string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var trimmed = query.Trim();
        var separatorIndex = trimmed.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var key = trimmed[..separatorIndex].Trim().ToLowerInvariant();
        var value = trimmed[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (key is "module" or "bo")
        {
            filterType = key;
            filterValue = value;
            return true;
        }

        return false;
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

    private void FocusDetailsFacetList()
    {
        if (_detailFacetOptions.Count == 0)
        {
            return;
        }

        if (DetailsFacetList.SelectedIndex < 0)
        {
            DetailsFacetList.SelectedIndex = 0;
        }

        DetailsFacetList.Focus();
        DetailsFacetList.UpdateLayout();

        if (DetailsFacetList.ItemContainerGenerator.ContainerFromIndex(DetailsFacetList.SelectedIndex) is System.Windows.Controls.ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
        }
    }

    private void MoveFacetSelection(int delta)
    {
        if (_detailFacetOptions.Count == 0)
        {
            return;
        }

        var current = DetailsFacetList.SelectedIndex;
        if (current < 0)
        {
            DetailsFacetList.SelectedIndex = delta > 0 ? 0 : _detailFacetOptions.Count - 1;
            return;
        }

        var next = Math.Clamp(current + delta, 0, _detailFacetOptions.Count - 1);
        DetailsFacetList.SelectedIndex = next;
    }

    private void ApplyRelatedFilterFromSelectedFacet()
    {
        if (DetailsFacetList.SelectedItem is not DetailFacetOption facet)
        {
            return;
        }

        _suppressPrefixSuggestions = true;
        _programmaticSearchUpdate = true;
        _searchDebounce.Stop();
        SearchBox.Text = facet.Query;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        _programmaticSearchUpdate = false;
        UpdateSearchHighlight(SearchBox.Text);
        ApplySearch(SearchBox.Text);
        EnsureListFocus();
        FocusSelectedResultItem();
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

        if (row.IsSuggestion)
        {
            ApplyBoSuggestionFilter(row);
            return;
        }

        var clipboardValue = $"/n{row.Code}";
        System.Windows.Clipboard.SetText(clipboardValue);

        HideLauncher();
        _trayIconService.ShowToast($"Code {row.Code} copied in the clipboard");
    }

    private void ApplyBoSuggestionFilter(ResultRowViewModel suggestion)
    {
        _suppressPrefixSuggestions = true;
        _programmaticSearchUpdate = true;
        _searchDebounce.Stop();
        SearchBox.Text = suggestion.SuggestionQuery;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        _programmaticSearchUpdate = false;
        UpdateSearchHighlight(SearchBox.Text);
        ApplySearch(SearchBox.Text);
        EnsureListFocus();
        FocusSelectedResultItem();
    }

    private void ConfigureWindow()
    {
        ConfigureWindow(WinForms.Control.MousePosition);
    }

    private void ConfigureWindow(DrawingPoint cursorPosition)
    {
        var targetScreen = WinForms.Screen.FromPoint(cursorPosition);
        var boundsPx = targetScreen.Bounds;
        var monitorScale = GetDpiScaleForPoint(cursorPosition);
        var windowHeightDip = boundsPx.Height / monitorScale;

        Topmost = true;
        WindowState = WindowState.Normal;

        if (!TrySetWindowBoundsInPixels(boundsPx))
        {
            Left = boundsPx.Left / monitorScale;
            Top = boundsPx.Top / monitorScale;
            Width = boundsPx.Width / monitorScale;
            Height = windowHeightDip;
        }

        var searchTop = Math.Max(24, windowHeightDip * 0.25);
        SearchHost.Margin = new Thickness(24, searchTop, 24, 24);

        const double searchHeight = 42;
        const double searchHostPaddingVertical = 40;
        const double verticalGap = 14;
        var resultsTop = searchTop + searchHeight + searchHostPaddingVertical + verticalGap;
        ResultsHost.Margin = new Thickness(24, resultsTop, 24, 24);
    }

    private bool TrySetWindowBoundsInPixels(System.Drawing.Rectangle boundsPx)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var ok = SetWindowPos(
            hwnd,
            new IntPtr(-1),
            boundsPx.Left,
            boundsPx.Top,
            boundsPx.Width,
            boundsPx.Height,
            SwpNoActivate | SwpNoOwnerZOrder);

        return ok;
    }

    private static double GetDpiScaleForPoint(DrawingPoint point)
    {
        var monitor = MonitorFromPoint(new POINT { X = point.X, Y = point.Y }, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return 1d;
        }

        var result = GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _);
        if (result != 0 || dpiX == 0)
        {
            return 1d;
        }

        return dpiX / 96d;
    }

    [DllImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void UpdateDetailsPanel()
    {
        if (ResultsList.SelectedItem is not ResultRowViewModel selected)
        {
            _detailFacetOptions.Clear();
            DetailsFacetList.Visibility = Visibility.Collapsed;
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
            return;
        }

        if (selected.IsSuggestion)
        {
            _detailFacetOptions.Clear();
            DetailsFacetList.Visibility = Visibility.Collapsed;
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
            return;
        }

        _detailFacetOptions.Clear();
        if (!string.IsNullOrWhiteSpace(selected.ModuleCode))
        {
            var moduleLabel = $"Module: {BuildModuleDetailsDisplay(selected.ModuleCode, selected.ModuleName)}";

            _detailFacetOptions.Add(new DetailFacetOption(moduleLabel, $"module:{selected.ModuleCode}"));
        }

        if (!string.IsNullOrWhiteSpace(selected.BusinessObjectCode))
        {
            var businessObjectLabel = $"BO: {selected.BusinessObjectCode}";
            if (!string.IsNullOrWhiteSpace(selected.BusinessObjectName))
            {
                businessObjectLabel += $" ({selected.BusinessObjectName})";
            }

            _detailFacetOptions.Add(new DetailFacetOption(businessObjectLabel, $"bo:{selected.BusinessObjectCode}"));
        }

        if (_detailFacetOptions.Count > 0)
        {
            DetailsFacetList.Visibility = Visibility.Visible;
            if (DetailsFacetList.SelectedIndex < 0 || DetailsFacetList.SelectedIndex >= _detailFacetOptions.Count)
            {
                DetailsFacetList.SelectedIndex = 0;
            }
        }
        else
        {
            DetailsFacetList.Visibility = Visibility.Collapsed;
        }

        DetailsTextBox.Text =
            $"Code: {selected.Code}{Environment.NewLine}" +
            $"Module: {BuildModuleDetailsDisplay(selected.ModuleCode, selected.ModuleName)}{Environment.NewLine}" +
            $"Keywords: {selected.Keywords}{Environment.NewLine}{Environment.NewLine}" +
            selected.LongDescription;

        DebugTextBox.Text = $"Debug ({selected.FilterText}): {selected.ScoreDebugText}";
    }

    private static string BuildModuleDetailsDisplay(string moduleCode, string moduleName)
    {
        var code = moduleCode?.Trim() ?? string.Empty;
        var name = moduleName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
        {
            return "-";
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return name;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return code;
        }

        if (string.Equals(code, name, StringComparison.OrdinalIgnoreCase))
        {
            return code;
        }

        if (name.Contains(code, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        if (code.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            return code;
        }

        return $"{code} - {name}";
    }

    private sealed record DetailFacetOption(string Label, string Query);

    private void TryEnableBlur()
    {
        BlurService.TryEnableBlur(this);
    }

    private async Task RefreshCacheIfNeededAsync(bool forceRefresh)
    {
        if (_isRefreshingCache)
        {
            return;
        }

        _isRefreshingCache = true;
        try
        {
            var cacheStatus = await _dataCacheService.EnsureFreshAsync(forceRefresh);

            if (forceRefresh || cacheStatus.DownloadSucceeded || _searchService.Count == 0)
            {
                _searchService.Reload();
                ApplySearch(SearchBox.Text);
            }

            UpdateCacheDebugInfo(cacheStatus);
        }
        catch
        {
            // Preserve existing data if refresh fails.
            CacheAgeRun.Text = "unavailable";
        }
        finally
        {
            _isRefreshingCache = false;
        }
    }

    private void UpdateCacheDebugInfo(DataCacheStatus cacheStatus)
    {
        CachePathRun.Text = cacheStatus.CacheFilePath;
        CacheAgeRun.Text = FormatAge(cacheStatus.Age);
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 1)
        {
            return $"{(int)age.TotalDays}d {age.Hours}h";
        }

        if (age.TotalHours >= 1)
        {
            return $"{(int)age.TotalHours}h {age.Minutes}m";
        }

        if (age.TotalMinutes >= 1)
        {
            return $"{(int)age.TotalMinutes}m";
        }

        return $"{Math.Max(0, (int)age.TotalSeconds)}s";
    }

    private async void CacheAgeHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink hyperlink)
        {
            hyperlink.IsEnabled = false;
        }

        try
        {
            await RefreshCacheIfNeededAsync(forceRefresh: true);
        }
        finally
        {
            if (sender is Hyperlink enabledHyperlink)
            {
                enabledHyperlink.IsEnabled = true;
            }
        }
    }
}
