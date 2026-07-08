namespace WxBridge.Core;

public sealed record MergedSnapshotEntryRequest(string OutputPath);

public sealed record MergedOpenEntryRequest(
    string SnapshotPath,
    int X,
    int Y,
    int W,
    int H);

public sealed record MergedOpenEntryAndSnapshotRequest(
    string EntrySnapshotPath,
    string OutputPath,
    int X,
    int Y,
    int W,
    int H,
    string? Index = null);

public sealed record MergedSnapshotPopupRequest(
    string OutputPath,
    string? WindowHandle = null,
    string? Index = null);

public sealed record MergedApplyPopupAnalysisRequest(
    string AnalysisPath,
    string? SnapshotPath = null,
    string? OutputPath = null,
    int? MaxItems = null,
    bool SkipImages = false);

public sealed record MergedApplyScrollSnapshotRequest(
    string AnalysisPath,
    string OutputPath,
    string? SnapshotPath = null,
    string? WindowHandle = null,
    int? MaxItems = null,
    bool SkipImages = false,
    int? Notches = null,
    int? Pixels = null,
    string? Index = null,
    bool NoScroll = false);

public sealed record MergedScrollPopupRequest(
    string? WindowHandle = null,
    int? Notches = null,
    int? Pixels = null);

public sealed record MergedRightClickImageRequest(
    string SnapshotPath,
    int X,
    int Y,
    int W,
    int H);

public sealed record MergedSnapshotScreenRequest(
    string OutputPath,
    string? Index = null);

public sealed record MergedClickRequest(
    string SnapshotPath,
    int X,
    int Y,
    int W,
    int H);

public sealed record MergedAppendClipboardImageRequest(
    string? SnapshotPath = null,
    string? OutputPath = null,
    string? Speaker = null);
