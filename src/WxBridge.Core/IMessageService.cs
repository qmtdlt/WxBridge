namespace WxBridge.Core;

public interface IMessageService
{
    OperationResult SendText(SendTextRequest request);

    OperationResult SendFile(SendFileRequest request);

    OperationResult SendClipboard();
}
