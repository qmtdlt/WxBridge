using WxBridge.Core;

namespace WxBridge.Windows;

internal sealed class WeChatActivator
{
    private readonly WeChatOptions _options;
    private readonly WeChatWindowLocator _windowLocator;

    public WeChatActivator(WeChatOptions options)
    {
        _options = options;
        _windowLocator = new WeChatWindowLocator(options);
    }

    public OperationResult Activate(string action)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure(action, "windows_required");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure(action, "wechat_window_not_found");
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);

        return OperationResult.Success(action, new { hWnd = hWnd.ToInt64() });
    }
}
