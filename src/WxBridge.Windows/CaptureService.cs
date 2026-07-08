using System.Runtime.Versioning;
using WxBridge.Core;

namespace WxBridge.Windows;

[SupportedOSPlatform("windows6.1")]
public sealed class CaptureService : ICaptureService
{
    private readonly WxBridgeOptions _options;
    private readonly WeChatWindowLocator _windowLocator;
    private readonly object _interactionSync = new();

    public CaptureService(WxBridgeOptions options)
    {
        _options = options;
        _windowLocator = new WeChatWindowLocator(options.WeChat);
    }

    public OperationResult Start(CaptureStartRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return OperationResult.Failure("capture.start", "output_path_is_empty");
        }

        var writer = new MarkdownCaptureWriter(request.OutputPath);
        Console.WriteLine("WxBridge capture started.");
        Console.WriteLine($"Output: {Path.GetFullPath(request.OutputPath)}");
        Console.WriteLine("Manual flow: right-click another avatar and click @ to set speaker; right-click your own avatar, then left-click away to set self; right-click message and click copy to append content.");
        Console.WriteLine("Press Ctrl+C to stop.");

        PollMouseActions((sequenceBeforeClick, rightClickPoint) =>
        {
            HandleMenuClick(sequenceBeforeClick, rightClickPoint, writer);
        }, cancellationToken);

        return OperationResult.Success("capture.start", new { output = Path.GetFullPath(request.OutputPath) });
    }

    private static void PollMouseActions(Action<uint, NativeMethods.Point?> onRightThenLeft, CancellationToken cancellationToken)
    {
        var rightWasDown = IsKeyDown(NativeMethods.VkRButton);
        var leftWasDown = IsKeyDown(NativeMethods.VkLButton);
        var rightClickPending = false;
        uint sequenceBeforeRightClick = 0;
        NativeMethods.Point? rightClickPoint = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var rightDown = IsKeyDown(NativeMethods.VkRButton);
            var leftDown = IsKeyDown(NativeMethods.VkLButton);

            if (rightWasDown && !rightDown)
            {
                sequenceBeforeRightClick = WindowsClipboard.GetSequenceNumber();
                rightClickPoint = NativeMethods.GetCursorPos(out var point) ? point : null;
                rightClickPending = true;
            }

            if (leftWasDown && !leftDown && rightClickPending)
            {
                rightClickPending = false;
                var sequence = sequenceBeforeRightClick;
                var point = rightClickPoint;
                Task.Run(() => onRightThenLeft(sequence, point), cancellationToken);
            }

            rightWasDown = rightDown;
            leftWasDown = leftDown;
            Thread.Sleep(30);
        }
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private void HandleMenuClick(uint sequenceBeforeClick, NativeMethods.Point? rightClickPoint, MarkdownCaptureWriter writer)
    {
        lock (_interactionSync)
        {
            if (IsSelfAvatarClick(rightClickPoint))
            {
                var selfSpeaker = _options.Markdown.SelfSpeakerName.Trim();
                selfSpeaker = string.IsNullOrWhiteSpace(selfSpeaker) ? "我" : selfSpeaker;
                writer.SetSpeaker(selfSpeaker);
                Console.WriteLine($"[speaker:self] {selfSpeaker}");
                return;
            }

            if (IsOtherAvatarClick(rightClickPoint))
            {
                var avatarSpeaker = TryReadSpeakerFromInput();
                if (!string.IsNullOrWhiteSpace(avatarSpeaker))
                {
                    writer.SetSpeaker(avatarSpeaker);
                    Console.WriteLine($"[speaker] {avatarSpeaker}");
                    return;
                }

                Console.WriteLine("[speaker] no @ mention was detected after avatar menu click.");
                return;
            }

            if (TryHandleClipboardContent(sequenceBeforeClick, writer))
            {
                return;
            }

            var speaker = TryReadSpeakerFromInput();
            if (!string.IsNullOrWhiteSpace(speaker))
            {
                writer.SetSpeaker(speaker);
                Console.WriteLine($"[speaker] {speaker}");
                return;
            }

            Console.WriteLine("[speaker] no clipboard change, but no @ mention was detected.");
        }
    }

    private bool TryHandleClipboardContent(uint sequenceBeforeClick, MarkdownCaptureWriter writer)
    {
        if (!WaitForClipboardChange(sequenceBeforeClick))
        {
            return false;
        }

        using var snapshot = WindowsClipboard.Capture();
        if (!string.IsNullOrWhiteSpace(snapshot.Text))
        {
            writer.AppendText(snapshot.Text);
            Console.WriteLine($"[content:text] {TrimForLog(snapshot.Text)}");
            return true;
        }

        if (snapshot.Image is not null)
        {
            writer.AppendImage(snapshot.Image);
            Console.WriteLine("[content:image] saved");
            return true;
        }

        Console.WriteLine("[clipboard] changed, but no supported text/image content was found.");
        return true;
    }

    private bool WaitForClipboardChange(uint sequenceBeforeClick)
    {
        var waitMs = Math.Max(0, _options.WeChat.CaptureClipboardWaitMs);
        var pollMs = Math.Max(10, _options.WeChat.CaptureSpeakerReadPollMs);
        var deadline = Environment.TickCount64 + waitMs;

        do
        {
            if (WindowsClipboard.GetSequenceNumber() != sequenceBeforeClick)
            {
                return true;
            }

            Thread.Sleep(Math.Min(pollMs, 25));
        }
        while (Environment.TickCount64 < deadline);

        return WindowsClipboard.GetSequenceNumber() != sequenceBeforeClick;
    }

    private string? TryReadSpeakerFromInput()
    {
        var timeoutMs = Math.Max(40, _options.WeChat.CaptureSpeakerReadTimeoutMs);
        var pollMs = Math.Max(10, _options.WeChat.CaptureSpeakerReadPollMs);
        var keyDelayMs = Math.Max(0, _options.WeChat.CaptureInputKeyDelayMs);
        var deadline = Environment.TickCount64 + timeoutMs;

        do
        {
            KeyboardInputDriver.SelectAll();
            Thread.Sleep(keyDelayMs);
            KeyboardInputDriver.Copy();
            Thread.Sleep(keyDelayMs);

            var value = WindowsClipboard.GetText().Trim();
            var speaker = ExtractLastMention(value);
            if (!string.IsNullOrWhiteSpace(speaker))
            {
                KeyboardInputDriver.SelectAll();
                Thread.Sleep(keyDelayMs);
                KeyboardInputDriver.Backspace();
                Thread.Sleep(keyDelayMs);
                return speaker;
            }

            Thread.Sleep(pollMs);
        }
        while (Environment.TickCount64 < deadline);

        return null;
    }

    private bool IsSelfAvatarClick(NativeMethods.Point? point)
    {
        return IsAvatarBandClick(point, "right");
    }

    private bool IsOtherAvatarClick(NativeMethods.Point? point)
    {
        return IsAvatarBandClick(point, "left");
    }

    private bool IsAvatarBandClick(NativeMethods.Point? point, string side)
    {
        if (point is null)
        {
            return false;
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hWnd, out var windowRect))
        {
            return false;
        }

        var regionLeft = windowRect.Left + _options.WeChat.ChatRegionLeftOffset;
        var regionTop = windowRect.Top + _options.WeChat.ChatRegionTopOffset;
        var regionRight = windowRect.Right - _options.WeChat.ChatRegionRightOffset;
        var regionBottom = windowRect.Bottom - _options.WeChat.ChatRegionBottomOffset;
        var bandWidth = Math.Max(_options.WeChat.VisibleExportAvatarSize, _options.WeChat.VisibleExportAvatarBandWidth);

        if (point.Value.Y < regionTop || point.Value.Y > regionBottom)
        {
            return false;
        }

        return side == "right"
            ? point.Value.X >= regionRight - bandWidth && point.Value.X <= regionRight
            : point.Value.X >= regionLeft && point.Value.X <= regionLeft + bandWidth;
    }

    private static string? ExtractLastMention(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts[^1];
    }

    private static string TrimForLog(string value)
    {
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 60 ? normalized : normalized[..60] + "...";
    }
}
