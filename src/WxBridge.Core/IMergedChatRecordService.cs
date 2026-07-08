namespace WxBridge.Core;

public interface IMergedChatRecordService
{
    OperationResult SnapshotEntry(MergedSnapshotEntryRequest request);

    OperationResult OpenEntry(MergedOpenEntryRequest request);

    OperationResult OpenEntryAndSnapshot(MergedOpenEntryAndSnapshotRequest request);

    OperationResult SnapshotPopup(MergedSnapshotPopupRequest request);

    OperationResult ApplyPopupAnalysis(MergedApplyPopupAnalysisRequest request);

    OperationResult ApplyScrollSnapshot(MergedApplyScrollSnapshotRequest request);

    OperationResult ScrollPopup(MergedScrollPopupRequest request);

    OperationResult RightClickImage(MergedRightClickImageRequest request);

    OperationResult SnapshotScreen(MergedSnapshotScreenRequest request);

    OperationResult Click(MergedClickRequest request);

    OperationResult AppendClipboardImage(MergedAppendClipboardImageRequest request);
}
