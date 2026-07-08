using System.Text;

namespace WxBridge.Windows;

internal sealed class WeChatWindowLocator
{
    private readonly WeChatOptions _options;

    public WeChatWindowLocator(WeChatOptions options)
    {
        _options = options;
    }

    public IntPtr FindMainWindow()
    {
        IntPtr matched = IntPtr.Zero;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            var title = GetTitle(hWnd);
            if (title.Contains(_options.WindowTitleKeyword, StringComparison.OrdinalIgnoreCase))
            {
                matched = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return matched;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var buffer = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
