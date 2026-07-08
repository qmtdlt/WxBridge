using System.Runtime.InteropServices;
using WxBridge.Core;

namespace WxBridge.Windows;

public sealed class CoordinateChatSessionService : IChatSessionService
{
    private readonly WeChatOptions _options;
    private readonly WeChatWindowLocator _windowLocator;
    private readonly UiaSessionInspector _inspector;

    public CoordinateChatSessionService(WxBridgeOptions options)
    {
        _options = options.WeChat;
        _windowLocator = new WeChatWindowLocator(_options);
        _inspector = new UiaSessionInspector(_options);
    }

    public OperationResult ListSessions()
    {
        return OperationResult.Failure(
            "sessions.list",
            "not_supported_yet",
            new
            {
                reason = "Listing sessions requires UI Automation or OCR. The first scaffold keeps the API contract ready."
            });
    }

    public OperationResult InspectSessions(InspectSessionsRequest request)
    {
        return _inspector.Inspect(request);
    }

    public OperationResult SwitchSession(SwitchSessionRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("sessions.switch", "windows_required");
        }

        if (request.Index <= 0)
        {
            return OperationResult.Failure("sessions.switch", "index_must_be_positive");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure("sessions.switch", "wechat_window_not_found");
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);

        if (!NativeMethods.GetWindowRect(hWnd, out var rect))
        {
            return OperationResult.Failure(
                "sessions.switch",
                "get_window_rect_failed",
                new { win32Error = Marshal.GetLastWin32Error() });
        }

        var x = rect.Left + _options.ChatListClickXOffset;
        var y = rect.Top + _options.ChatListTopYOffset + ((request.Index - 1) * _options.ChatItemHeight) + (_options.ChatItemHeight / 2);

        MouseInputDriver.Click(x, y);
        Thread.Sleep(_options.ClickDelayMs);

        return OperationResult.Success(
            "sessions.switch",
            new
            {
                index = request.Index,
                clicked = new { x, y },
                strategy = "coordinate"
            });
    }

    public OperationResult SearchSession(SearchSessionRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("sessions.search", "windows_required");
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return OperationResult.Failure("sessions.search", "query_is_empty");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure("sessions.search", "wechat_window_not_found");
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);

        if (!NativeMethods.GetWindowRect(hWnd, out var rect))
        {
            return OperationResult.Failure(
                "sessions.search",
                "get_window_rect_failed",
                new { win32Error = Marshal.GetLastWin32Error() });
        }

        var x = rect.Left + _options.SearchBoxClickXOffset;
        var y = rect.Top + _options.SearchBoxClickYOffset;

        try
        {
            using var clipboardSnapshot = WindowsClipboard.Capture();
            var clickCount = Math.Max(1, _options.SearchBoxClickCount);
            for (var i = 0; i < clickCount; i++)
            {
                MouseInputDriver.Click(x, y);
                Thread.Sleep(_options.SearchBoxClickGapMs);
            }

            Thread.Sleep(_options.SearchFocusPauseMs);
            KeyboardInputDriver.SelectAll();
            Thread.Sleep(_options.ClickDelayMs);
            WindowsClipboard.SetText(request.Query.Trim());
            KeyboardInputDriver.Paste();
            Thread.Sleep(_options.ClickDelayMs);
            WindowsClipboard.Restore(clipboardSnapshot);
            Thread.Sleep(_options.SearchKeywordPauseMs);
            Thread.Sleep(_options.SearchResultDelayMs);
            KeyboardInputDriver.Enter();
            Thread.Sleep(_options.SearchOpenDelayMs);

            return OperationResult.Success(
                "sessions.search",
                new
                {
                    query = request.Query.Trim(),
                    searchBox = new { x, y },
                    strategy = "search_enter_first_result"
                });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("sessions.search", "search_failed", new { ex.Message });
        }
    }
}
