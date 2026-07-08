namespace WxBridge.Core;

public interface ICaptureService
{
    OperationResult Start(CaptureStartRequest request, CancellationToken cancellationToken);
}
