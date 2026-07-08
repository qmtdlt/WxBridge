using WxBridge.Core;

namespace WxBridge.Windows;

public sealed class ClipboardMessageService : IMessageService
{
    private readonly WeChatOptions _options;
    private readonly WeChatActivator _activator;

    public ClipboardMessageService(WxBridgeOptions options)
    {
        _options = options.WeChat;
        _activator = new WeChatActivator(_options);
    }

    public OperationResult SendText(SendTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return OperationResult.Failure("messages.send_text", "text_is_empty");
        }

        try
        {
            WindowsClipboard.SetText(request.Text);
            return PasteAndSend("messages.send_text", new { kind = "text", length = request.Text.Length });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("messages.send_text", "send_failed", new { ex.Message });
        }
    }

    public OperationResult SendFile(SendFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return OperationResult.Failure("messages.send_file", "path_is_empty");
        }

        if (!File.Exists(request.Path))
        {
            return OperationResult.Failure("messages.send_file", "file_not_found", new { path = request.Path });
        }

        try
        {
            var fullPath = Path.GetFullPath(request.Path);
            WindowsClipboard.SetFileDropList(fullPath);
            return PasteAndSend("messages.send_file", new { kind = "file", path = fullPath });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("messages.send_file", "send_failed", new { ex.Message });
        }
    }

    public OperationResult SendClipboard()
    {
        try
        {
            return PasteAndSend("messages.send_clipboard", new { kind = "clipboard" });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("messages.send_clipboard", "send_failed", new { ex.Message });
        }
    }

    private OperationResult PasteAndSend(string action, object data)
    {
        var activation = _activator.Activate(action);
        if (!activation.Ok)
        {
            return activation;
        }

        KeyboardInputDriver.Paste();
        Thread.Sleep(_options.PasteDelayMs);
        KeyboardInputDriver.Enter();
        Thread.Sleep(_options.SendDelayMs);

        return OperationResult.Success(action, data);
    }
}
