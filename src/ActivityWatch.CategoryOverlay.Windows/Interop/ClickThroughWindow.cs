using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ActivityWatch.CategoryOverlay.Windows.Interop;

public static class ClickThroughWindow
{
    private const int ExtendedStyleIndex = -20;
    private const long Transparent = 0x00000020L;
    private const long ToolWindow = 0x00000080L;
    private const long Layered = 0x00080000L;

    public static void Apply(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("The window handle is not initialized.");
        }

        var current = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        var updated = current | ToolWindow | Layered;
        updated = enabled ? updated | Transparent : updated & ~Transparent;
        SetWindowLongPtr(handle, ExtendedStyleIndex, new IntPtr(updated));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(
        IntPtr windowHandle,
        int index,
        IntPtr newValue);
}

