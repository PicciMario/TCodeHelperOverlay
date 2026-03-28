using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TCodeLaunchpad.App.Services;

internal static class BlurService
{
    private const int DwmWindowAttributeSystemBackdropType = 38;
    private const int DwmBackdropMainWindow = 2;

    public static void TryEnableBlur(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var backdrop = DwmBackdropMainWindow;
            DwmSetWindowAttribute(hwnd, DwmWindowAttributeSystemBackdropType, ref backdrop, Marshal.SizeOf<int>());
        }
        catch
        {
            // Keep fallback translucent overlay when OS API is not available.
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
