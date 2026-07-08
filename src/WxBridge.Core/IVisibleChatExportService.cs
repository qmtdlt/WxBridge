namespace WxBridge.Core;

public interface IVisibleChatExportService
{
    OperationResult ExportVisible(ExportVisibleChatRequest request);

    OperationResult SnapshotVisible(SnapshotVisibleChatRequest request);

    OperationResult ApplyVisibleAnalysis(ApplyVisibleChatAnalysisRequest request);
}
