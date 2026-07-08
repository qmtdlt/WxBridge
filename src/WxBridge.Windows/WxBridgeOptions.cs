namespace WxBridge.Windows;

public sealed class WxBridgeOptions
{
    public WeChatOptions WeChat { get; set; } = new();

    public MarkdownOptions Markdown { get; set; } = new();
}

public sealed class MarkdownOptions
{
    public string? OutputDirectory { get; set; }

    public string? OutputName { get; set; }

    public string SelfSpeakerName { get; set; } = "我";
}

public sealed class WeChatOptions
{
    public string WindowTitleKeyword { get; set; } = "微信";

    public string MergedPopupTitleKeyword { get; set; } = "聊天记录";

    public int ChatListClickXOffset { get; set; } = 180;

    public int ChatListTopYOffset { get; set; } = 92;

    public int ChatItemHeight { get; set; } = 64;

    public int SearchBoxClickXOffset { get; set; } = 180;

    public int SearchBoxClickYOffset { get; set; } = 48;

    public int SearchBoxClickCount { get; set; } = 2;

    public int SearchBoxClickGapMs { get; set; } = 90;

    public int SearchFocusPauseMs { get; set; } = 80;

    public int SearchKeywordPauseMs { get; set; } = 120;

    public int SearchResultDelayMs { get; set; } = 300;

    public int SearchOpenDelayMs { get; set; } = 180;

    public int ActivateDelayMs { get; set; } = 120;

    public int ClickDelayMs { get; set; } = 80;

    public int PasteDelayMs { get; set; } = 120;

    public int SendDelayMs { get; set; } = 120;

    public int ChatRegionLeftOffset { get; set; } = 300;

    public int ChatRegionTopOffset { get; set; } = 80;

    public int ChatRegionRightOffset { get; set; } = 0;

    public int ChatRegionBottomOffset { get; set; } = 205;

    public int VisibleExportLeftInset { get; set; } = 20;

    public int VisibleExportRightInset { get; set; } = 10;

    public int VisibleExportScanStep { get; set; } = 5;

    public int VisibleExportAvatarSize { get; set; } = 36;

    public int VisibleExportAvatarBandWidth { get; set; } = 80;

    public int VisibleExportRightGutterWidth { get; set; } = 24;

    public int VisibleExportCandidateMinGap { get; set; } = 40;

    public int VisibleExportCandidateTopSafeMargin { get; set; } = 8;

    public int VisibleExportCandidateBottomSafeMargin { get; set; } = 8;

    public int InputBoxClickYOffsetFromWindowBottom { get; set; } = 70;

    public int AvatarMentionClickLeftOffsetX { get; set; } = 80;

    public int AvatarMentionClickRightOffsetX { get; set; } = -90;

    public int AvatarMentionClickOffsetY { get; set; } = 18;

    public int MessageCopyClickLeftOffsetX { get; set; } = 90;

    public int MessageCopyClickRightOffsetX { get; set; } = -90;

    public int[] MessageCopyClickOffsetYs { get; set; } = [34, 58, 82, 106];

    public int ContextMenuCopyClickOffsetX { get; set; } = 50;

    public int ContextMenuCopyClickOffsetY { get; set; } = 18;

    public int InteractionDelayMs { get; set; } = 120;

    public int VisibleExportImageCopyWaitMs { get; set; } = 220;

    public int MergedPopupOpenWaitMs { get; set; } = 500;

    public int MergedPopupScrollDelayMs { get; set; } = 250;

    public int CaptureClipboardWaitMs { get; set; } = 120;

    public int CaptureSpeakerReadTimeoutMs { get; set; } = 220;

    public int CaptureSpeakerReadPollMs { get; set; } = 25;

    public int CaptureInputKeyDelayMs { get; set; } = 20;
}
