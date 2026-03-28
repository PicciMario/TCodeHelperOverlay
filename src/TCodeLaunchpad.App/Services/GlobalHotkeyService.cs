using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TCodeLaunchpad.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ModControl = 0x0002;
    private const int VkSpace = 0x20;

    private readonly IntPtr _hwnd;
    private readonly int _hotkeyId;
    private readonly HwndSource? _source;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyService(Window window, int hotkeyId = 42)
    {
        _hotkeyId = hotkeyId;
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HookWndProc);
    }

    public bool TryRegisterCtrlSpace(out int errorCode)
    {
        _isRegistered = RegisterHotKey(_hwnd, _hotkeyId, ModControl, VkSpace);
        errorCode = _isRegistered ? 0 : Marshal.GetLastWin32Error();
        return _isRegistered;
    }

    public void Dispose()
    {
        if (_isRegistered)
        {
            UnregisterHotKey(_hwnd, _hotkeyId);
        }

        _source?.RemoveHook(HookWndProc);
    }

    private IntPtr HookWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
