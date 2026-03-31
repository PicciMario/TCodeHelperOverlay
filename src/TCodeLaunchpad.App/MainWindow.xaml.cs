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

    private readonly ObservableCollection<ResultRowViewModel> _rows = new();
    private readonly DataCacheService _dataCacheService;
    private readonly TransactionSearchService _searchService;
    private GlobalHotkeyService? _hotkeyService;
    private readonly TrayIconService _trayIconService;
    private readonly DispatcherTimer _searchDebounce;
    private bool _isExiting;
    private bool _isRefreshingCache;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        TryEnableBlur();

        ResultsList.ItemsSource = _rows;

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
            DetailsTextBox.Text = string.Empty;
            DebugTextBox.Text = string.Empty;
            return;
        }

        DetailsTextBox.Text =
            $"Code: {selected.Code}{Environment.NewLine}" +
            $"Module: {selected.Module}{Environment.NewLine}" +
            $"Business Object: {(string.IsNullOrWhiteSpace(selected.BusinessObjectName) ? "-" : selected.BusinessObjectName)}{Environment.NewLine}" +
            $"Keywords: {selected.Keywords}{Environment.NewLine}{Environment.NewLine}" +
            selected.LongDescription;

        DebugTextBox.Text = $"Debug ({selected.FilterText}): {selected.ScoreDebugText}";
    }

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

            if (cacheStatus.DownloadSucceeded || _searchService.Count == 0)
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
