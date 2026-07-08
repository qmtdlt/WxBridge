namespace WxBridge.Core;

public sealed record ExportVisibleChatRequest(
    string OutputPath,
    int? LeftInset = null,
    int? RightInset = null,
    int? ScanStep = null,
    bool ResolveNames = false,
    bool CopyContent = false,
    int? MaxItems = null);

public sealed record SnapshotVisibleChatRequest(string OutputPath);

public sealed record ApplyVisibleChatAnalysisRequest(
    string AnalysisPath,
    string? SnapshotPath = null,
    string? OutputPath = null,
    int? MaxItems = null);
