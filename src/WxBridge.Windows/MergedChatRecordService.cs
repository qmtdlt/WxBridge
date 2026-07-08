using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using WxBridge.Core;

namespace WxBridge.Windows;

[SupportedOSPlatform("windows6.1")]
public sealed class MergedChatRecordService : IMergedChatRecordService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly WeChatOptions _options;
    private readonly MarkdownOptions _markdownOptions;
    private readonly WeChatWindowLocator _windowLocator;

    public MergedChatRecordService(WxBridgeOptions options)
    {
        _options = options.WeChat;
        _markdownOptions = options.Markdown;
        _windowLocator = new WeChatWindowLocator(_options);
    }

    public OperationResult SnapshotEntry(MergedSnapshotEntryRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.snapshot_entry", "windows_required");
        }

        var mainWindow = _windowLocator.FindMainWindow();
        if (mainWindow == IntPtr.Zero)
        {
            return OperationResult.Failure("merged.snapshot_entry", "wechat_window_not_found");
        }

        EnsureForeground(mainWindow);
        if (!NativeMethods.GetWindowRect(mainWindow, out var windowRect))
        {
            return OperationResult.Failure("merged.snapshot_entry", "get_window_rect_failed");
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        var region = CalculateChatRegion(windowRect);
        if (region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure("merged.snapshot_entry", "invalid_chat_region", new { region });
        }

        var snapshot = SaveSnapshot(
            outputPath,
            region,
            "entry",
            null,
            "merged-entry-snapshot",
            null);

        return OperationResult.Success(
            "merged.snapshot_entry",
            SnapshotResult(snapshot));
    }

    public OperationResult OpenEntry(MergedOpenEntryRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.open_entry", "windows_required");
        }

        var snapshot = LoadSnapshot(request.SnapshotPath);
        if (snapshot is null)
        {
            return OperationResult.Failure("merged.open_entry", "snapshot_not_found", new { snapshot = request.SnapshotPath });
        }

        var region = snapshot.Region.ToRectangle();
        if (request.W <= 0 || request.H <= 0)
        {
            return OperationResult.Failure("merged.open_entry", "invalid_bbox", new { request.X, request.Y, request.W, request.H });
        }

        var before = EnumerateVisibleWindows()
            .Select(window => window.Handle)
            .ToHashSet();
        var clickX = region.X + request.X + (request.W / 2);
        var clickY = region.Y + request.Y + (request.H / 2);
        MouseInputDriver.Click(clickX, clickY);
        Thread.Sleep(_options.MergedPopupOpenWaitMs);

        var popup = FindPopupWindow(before);
        if (popup == IntPtr.Zero)
        {
            return OperationResult.Failure(
                "merged.open_entry",
                "merged_popup_not_found",
                new { click = new { x = clickX, y = clickY }, titleKeyword = _options.MergedPopupTitleKeyword });
        }

        EnsureForeground(popup);
        return OperationResult.Success(
            "merged.open_entry",
            new
            {
                windowHandle = FormatHandle(popup),
                title = GetTitle(popup),
                click = new { x = clickX, y = clickY },
                bbox = new { request.X, request.Y, request.W, request.H }
            });
    }

    public OperationResult OpenEntryAndSnapshot(MergedOpenEntryAndSnapshotRequest request)
    {
        var open = OpenEntry(new MergedOpenEntryRequest(
            request.EntrySnapshotPath,
            request.X,
            request.Y,
            request.W,
            request.H));
        if (!open.Ok)
        {
            return OperationResult.Failure("merged.open_entry_and_snapshot", open.Error ?? "open_entry_failed", open.Data);
        }

        var windowHandle = GetStringDataProperty(open.Data, "windowHandle");
        var snapshot = SnapshotPopup(new MergedSnapshotPopupRequest(
            request.OutputPath,
            windowHandle,
            request.Index));
        if (!snapshot.Ok)
        {
            return OperationResult.Failure(
                "merged.open_entry_and_snapshot",
                snapshot.Error ?? "snapshot_popup_failed",
                new { open = open.Data, snapshot = snapshot.Data });
        }

        return OperationResult.Success(
            "merged.open_entry_and_snapshot",
            new
            {
                open = open.Data,
                snapshot = snapshot.Data
            });
    }

    public OperationResult SnapshotPopup(MergedSnapshotPopupRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.snapshot_popup", "windows_required");
        }

        var popup = ResolvePopupWindow(request.WindowHandle);
        if (popup == IntPtr.Zero)
        {
            return OperationResult.Failure(
                "merged.snapshot_popup",
                "merged_popup_not_found",
                new { request.WindowHandle, titleKeyword = _options.MergedPopupTitleKeyword });
        }

        EnsureForeground(popup);
        if (!NativeMethods.GetWindowRect(popup, out var rect))
        {
            return OperationResult.Failure("merged.snapshot_popup", "get_window_rect_failed", new { windowHandle = FormatHandle(popup) });
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        var region = ToRectangle(rect);
        if (region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure("merged.snapshot_popup", "invalid_popup_region", new { region });
        }

        var snapshot = SaveSnapshot(
            outputPath,
            region,
            "popup",
            popup,
            "merged-popup-snapshot",
            request.Index);

        return OperationResult.Success(
            "merged.snapshot_popup",
            SnapshotResult(snapshot));
    }

    public OperationResult ScrollPopup(MergedScrollPopupRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.scroll_popup", "windows_required");
        }

        var popup = ResolvePopupWindow(request.WindowHandle);
        if (popup == IntPtr.Zero)
        {
            return OperationResult.Failure(
                "merged.scroll_popup",
                "merged_popup_not_found",
                new { request.WindowHandle, titleKeyword = _options.MergedPopupTitleKeyword });
        }

        EnsureForeground(popup);
        if (!NativeMethods.GetWindowRect(popup, out var rect))
        {
            return OperationResult.Failure("merged.scroll_popup", "get_window_rect_failed", new { windowHandle = FormatHandle(popup) });
        }

        var region = ToRectangle(rect);
        var scrollPoint = GetPopupScrollPoint(region);
        var requestedPixels = request.Pixels;
        var wheelNotches = request.Notches
            ?? (request.Pixels.HasValue
                ? PixelsToWheelNotches(request.Pixels.Value)
                : CalculateDefaultPopupScrollNotches(region));
        MouseInputDriver.MoveTo(scrollPoint.X, scrollPoint.Y);
        Thread.Sleep(_options.InteractionDelayMs);
        MouseInputDriver.WheelRepeated(wheelNotches, 8);
        Thread.Sleep(_options.MergedPopupScrollDelayMs);

        return OperationResult.Success(
            "merged.scroll_popup",
            new
            {
                windowHandle = FormatHandle(popup),
                title = GetTitle(popup),
                requestedPixels,
                strategy = request.Notches.HasValue
                    ? "explicit_notches"
                    : request.Pixels.HasValue
                        ? "pixels_to_notches"
                        : "height_bucket_notches",
                wheelNotches,
                scrollPoint = new { scrollPoint.X, scrollPoint.Y },
                region = new { region.X, region.Y, region.Width, region.Height }
            });
    }

    public OperationResult ApplyScrollSnapshot(MergedApplyScrollSnapshotRequest request)
    {
        var apply = ApplyPopupAnalysis(new MergedApplyPopupAnalysisRequest(
            request.AnalysisPath,
            request.SnapshotPath,
            request.OutputPath,
            request.MaxItems,
            request.SkipImages));
        if (!apply.Ok)
        {
            return OperationResult.Failure("merged.apply_scroll_snapshot", apply.Error ?? "apply_popup_analysis_failed", apply.Data);
        }

        if (request.NoScroll)
        {
            return OperationResult.Success(
                "merged.apply_scroll_snapshot",
                new
                {
                    apply = apply.Data,
                    scroll = (object?)null,
                    snapshot = (object?)null
                });
        }

        var windowHandle = request.WindowHandle
            ?? GetStringDataProperty(apply.Data, "windowHandle");
        var scroll = ScrollPopup(new MergedScrollPopupRequest(
            windowHandle,
            request.Notches,
            request.Pixels));
        if (!scroll.Ok)
        {
            return OperationResult.Failure(
                "merged.apply_scroll_snapshot",
                scroll.Error ?? "scroll_popup_failed",
                new { apply = apply.Data, scroll = scroll.Data });
        }

        windowHandle = GetStringDataProperty(scroll.Data, "windowHandle") ?? windowHandle;
        var snapshot = SnapshotPopup(new MergedSnapshotPopupRequest(
            request.OutputPath,
            windowHandle,
            request.Index));
        if (!snapshot.Ok)
        {
            return OperationResult.Failure(
                "merged.apply_scroll_snapshot",
                snapshot.Error ?? "snapshot_popup_failed",
                new { apply = apply.Data, scroll = scroll.Data, snapshot = snapshot.Data });
        }

        return OperationResult.Success(
            "merged.apply_scroll_snapshot",
            new
            {
                apply = apply.Data,
                scroll = scroll.Data,
                snapshot = snapshot.Data
            });
    }

    public OperationResult RightClickImage(MergedRightClickImageRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.right_click_image", "windows_required");
        }

        var snapshot = LoadSnapshot(request.SnapshotPath);
        if (snapshot is null)
        {
            return OperationResult.Failure("merged.right_click_image", "snapshot_not_found", new { snapshot = request.SnapshotPath });
        }

        if (request.W <= 0 || request.H <= 0)
        {
            return OperationResult.Failure("merged.right_click_image", "invalid_bbox", new { request.X, request.Y, request.W, request.H });
        }

        var region = snapshot.Region.ToRectangle();
        var imageRect = new Rectangle(region.X + request.X, region.Y + request.Y, request.W, request.H);
        if (request.X < 0
            || request.Y < 0
            || request.X + request.W > region.Width
            || request.Y + request.H > region.Height)
        {
            return OperationResult.Failure("merged.right_click_image", "bbox_outside_screenshot", new { request.X, request.Y, request.W, request.H, region });
        }

        var target = ResolveTargetWindow(snapshot.WindowHandle, IntPtr.Zero);
        if (target != IntPtr.Zero)
        {
            EnsureForeground(target);
        }

        var anchor = new Point(Math.Max(imageRect.Left, imageRect.Right - 8), imageRect.Top + (imageRect.Height / 2));
        MouseInputDriver.RightClick(anchor.X, anchor.Y);
        Thread.Sleep(_options.InteractionDelayMs);

        return OperationResult.Success(
            "merged.right_click_image",
            new
            {
                snapshot = Path.GetFullPath(request.SnapshotPath),
                windowHandle = target == IntPtr.Zero ? snapshot.WindowHandle : FormatHandle(target),
                image = new { imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height },
                anchor = new { anchor.X, anchor.Y }
            });
    }

    public OperationResult SnapshotScreen(MergedSnapshotScreenRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.snapshot_screen", "windows_required");
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        var region = SystemInformation.VirtualScreen;
        if (region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure("merged.snapshot_screen", "invalid_virtual_screen", new { region });
        }

        var snapshot = SaveSnapshot(
            outputPath,
            region,
            "screen",
            null,
            "merged-screen-snapshot",
            request.Index);

        return OperationResult.Success(
            "merged.snapshot_screen",
            SnapshotResult(snapshot));
    }

    public OperationResult Click(MergedClickRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.click", "windows_required");
        }

        var snapshot = LoadSnapshot(request.SnapshotPath);
        if (snapshot is null)
        {
            return OperationResult.Failure("merged.click", "snapshot_not_found", new { snapshot = request.SnapshotPath });
        }

        if (request.W <= 0 || request.H <= 0)
        {
            return OperationResult.Failure("merged.click", "invalid_bbox", new { request.X, request.Y, request.W, request.H });
        }

        var region = snapshot.Region.ToRectangle();
        if (request.X < 0
            || request.Y < 0
            || request.X + request.W > region.Width
            || request.Y + request.H > region.Height)
        {
            return OperationResult.Failure("merged.click", "bbox_outside_screenshot", new { request.X, request.Y, request.W, request.H, region });
        }

        var clickX = region.X + request.X + (request.W / 2);
        var clickY = region.Y + request.Y + (request.H / 2);
        MouseInputDriver.Click(clickX, clickY);
        Thread.Sleep(_options.InteractionDelayMs);

        return OperationResult.Success(
            "merged.click",
            new
            {
                snapshot = Path.GetFullPath(request.SnapshotPath),
                click = new { x = clickX, y = clickY },
                bbox = new { request.X, request.Y, request.W, request.H }
            });
    }

    public OperationResult AppendClipboardImage(MergedAppendClipboardImageRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.append_clipboard_image", "windows_required");
        }

        var snapshot = LoadSnapshot(request.SnapshotPath);
        var outputPath = Path.GetFullPath(request.OutputPath ?? snapshot?.Output ?? string.Empty);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return OperationResult.Failure(
                "merged.append_clipboard_image",
                "missing_output_path",
                new { option = "--output or --snapshot" });
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return OperationResult.Failure("merged.append_clipboard_image", "invalid_output_path", new { outputPath });
        }

        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outputPath) + "_assets");
        Directory.CreateDirectory(assetsDir);
        var statePath = Path.Combine(assetsDir, "merged-export-state.json");
        var state = LoadState(statePath);

        using var clipboard = WindowsClipboard.Capture();
        if (clipboard.Image is null)
        {
            return OperationResult.Failure("merged.append_clipboard_image", "clipboard_has_no_image");
        }

        var imageHash = HashImage(clipboard.Image);
        if (!state.ImageHashes.Add(imageHash))
        {
            SaveState(statePath, state);
            return OperationResult.Success(
                "merged.append_clipboard_image",
                new
                {
                    output = outputPath,
                    state = statePath,
                    skippedDuplicate = true,
                    imageHash
                });
        }

        var writer = new MarkdownCaptureWriter(outputPath);
        writer.SetSpeaker(string.IsNullOrWhiteSpace(request.Speaker) ? "图片" : request.Speaker.Trim());
        writer.AppendImage(clipboard.Image);
        SaveState(statePath, state);

        return OperationResult.Success(
            "merged.append_clipboard_image",
            new
            {
                output = outputPath,
                state = statePath,
                skippedDuplicate = false,
                imageHash
            });
    }

    public OperationResult ApplyPopupAnalysis(MergedApplyPopupAnalysisRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("merged.apply_popup_analysis", "windows_required");
        }

        if (string.IsNullOrWhiteSpace(request.AnalysisPath))
        {
            return OperationResult.Failure("merged.apply_popup_analysis", "analysis_path_is_empty");
        }

        var analysisPath = Path.GetFullPath(request.AnalysisPath);
        if (!File.Exists(analysisPath))
        {
            return OperationResult.Failure("merged.apply_popup_analysis", "analysis_not_found", new { analysis = analysisPath });
        }

        var analysis = LoadAnalysis(analysisPath);
        var snapshot = LoadSnapshot(request.SnapshotPath, analysis.Snapshot);
        var outputPath = Path.GetFullPath(request.OutputPath ?? analysis.Output ?? snapshot?.Output ?? string.Empty);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return OperationResult.Failure(
                "merged.apply_popup_analysis",
                "missing_output_path",
                new { option = "--output or analysis.output or snapshot.output" });
        }

        var region = analysis.Region ?? snapshot?.Region;
        if (region is null || region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure(
                "merged.apply_popup_analysis",
                "missing_region",
                new { required = "analysis.region or snapshot.region" });
        }

        var popup = ResolvePopupWindow(analysis.WindowHandle ?? snapshot?.WindowHandle);
        if (popup == IntPtr.Zero)
        {
            return OperationResult.Failure("merged.apply_popup_analysis", "target_window_not_found");
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return OperationResult.Failure("merged.apply_popup_analysis", "invalid_output_path", new { outputPath });
        }

        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outputPath) + "_assets");
        Directory.CreateDirectory(assetsDir);

        var statePath = Path.Combine(assetsDir, "merged-export-state.json");
        var state = LoadState(statePath);
        var writer = new MarkdownCaptureWriter(outputPath);
        var messages = NormalizeMessages(analysis, request.MaxItems).ToList();
        var copiedImages = 0;
        var failedImages = 0;
        var skippedImages = 0;
        var copiedTexts = 0;
        var failedTexts = 0;
        var fallbackTexts = 0;
        var writtenTexts = 0;
        var skippedDuplicates = 0;
        string? currentSpeaker = null;

        using var originalClipboard = WindowsClipboard.Capture();
        try
        {
            foreach (var message in messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var speaker = ResolveSpeaker(message);
                if (IsImageMessage(message))
                {
                    if (request.SkipImages)
                    {
                        skippedImages++;
                        continue;
                    }

                    popup = ResolveTargetWindow(analysis.WindowHandle ?? snapshot?.WindowHandle, popup);
                    if (TryCopyImageMessage(message, region.ToRectangle(), popup, out var copiedImage, out var error))
                    {
                        using (var image = copiedImage!)
                        {
                            var imageHash = HashImage(image);
                            if (!state.ImageHashes.Add(imageHash))
                            {
                                skippedDuplicates++;
                                continue;
                            }

                            if (!speaker.Equals(currentSpeaker, StringComparison.Ordinal))
                            {
                                writer.SetSpeaker(speaker);
                                currentSpeaker = speaker;
                            }

                            writer.AppendImage(image);
                            copiedImages++;
                        }
                    }
                    else
                    {
                        if (!speaker.Equals(currentSpeaker, StringComparison.Ordinal))
                        {
                            writer.SetSpeaker(speaker);
                            currentSpeaker = speaker;
                        }

                        failedImages++;
                        writer.AppendText($"> [图片复制失败，需要人工补充：{error}]");
                    }

                    continue;
                }

                popup = ResolveTargetWindow(analysis.WindowHandle ?? snapshot?.WindowHandle, popup);
                var copiedText = TryCopyTextMessage(message, region.ToRectangle(), popup, out _);
                var text = NormalizeCopiedText(copiedText);
                if (string.IsNullOrWhiteSpace(text))
                {
                    failedTexts++;
                    text = NormalizeCopiedText(message.Text);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        fallbackTexts++;
                    }
                }
                else
                {
                    copiedTexts++;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!state.TextKeys.Add(text))
                {
                    skippedDuplicates++;
                    continue;
                }

                if (!speaker.Equals(currentSpeaker, StringComparison.Ordinal))
                {
                    writer.SetSpeaker(speaker);
                    currentSpeaker = speaker;
                }

                writer.AppendText(text);
                writtenTexts++;
            }
        }
        finally
        {
            WindowsClipboard.Restore(originalClipboard);
        }

        SaveState(statePath, state);

        string? debugPath = null;
        var screenshotPath = analysis.Screenshot ?? snapshot?.Screenshot;
        if (!string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath))
        {
            debugPath = Path.Combine(assetsDir, "merged-popup-analysis-debug.png");
            using var screenshot = new Bitmap(screenshotPath);
            SaveDebugImage(screenshot, messages, debugPath);
        }

        return OperationResult.Success(
            "merged.apply_popup_analysis",
            new
            {
                output = outputPath,
                screenshot = screenshotPath,
                debug = debugPath,
                analysis = analysisPath,
                state = statePath,
                windowHandle = FormatHandle(popup),
                region,
                count = messages.Count,
                writtenTexts,
                copiedTexts,
                failedTexts,
                fallbackTexts,
                copiedImages,
                skippedImages,
                skippedDuplicates,
                failedImages
            });
    }

    private SnapshotManifest SaveSnapshot(
        string outputPath,
        Rectangle region,
        string windowKind,
        IntPtr? windowHandle,
        string fileStem,
        string? index)
    {
        var outputDir = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outputPath) + "_assets");
        Directory.CreateDirectory(assetsDir);

        var suffix = string.IsNullOrWhiteSpace(index) ? string.Empty : "-" + index.Trim();
        var screenshotPath = Path.Combine(assetsDir, $"{fileStem}{suffix}.png");
        using (var screenshot = Capture(region))
        {
            screenshot.Save(screenshotPath, ImageFormat.Png);
        }

        var snapshotPath = Path.Combine(assetsDir, $"{fileStem}{suffix}.json");
        var suggestedAnalysisPath = Path.Combine(assetsDir, $"{fileStem.Replace("snapshot", "analysis")}{suffix}.json");
        var manifest = new SnapshotManifest
        {
            Output = outputPath,
            Screenshot = screenshotPath,
            Region = new SnapshotRegion(region.X, region.Y, region.Width, region.Height),
            WindowKind = windowKind,
            WindowHandle = windowHandle is null ? null : FormatHandle(windowHandle.Value),
            ScreenshotHash = HashFile(screenshotPath),
            SuggestedAnalysis = suggestedAnalysisPath,
            Index = index
        };
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);
        manifest.Snapshot = snapshotPath;
        return manifest;
    }

    private object SnapshotResult(SnapshotManifest snapshot)
    {
        return new
        {
            output = snapshot.Output,
            screenshot = snapshot.Screenshot,
            snapshot = snapshot.Snapshot,
            suggestedAnalysis = snapshot.SuggestedAnalysis,
            windowKind = snapshot.WindowKind,
            windowHandle = snapshot.WindowHandle,
            screenshotHash = snapshot.ScreenshotHash,
            index = snapshot.Index,
            region = snapshot.Region,
            analysisSchema = new
            {
                output = snapshot.Output,
                snapshot = snapshot.Snapshot,
                screenshot = snapshot.Screenshot,
                windowHandle = snapshot.WindowHandle,
                region = snapshot.Region,
                copyPoints = new[]
                {
                    new
                    {
                        speaker = "sender name or empty",
                        type = "text|image|other",
                        x = 0,
                        y = 0
                    }
                }
            }
        };
    }

    private SnapshotManifest? LoadSnapshot(string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(snapshotPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<SnapshotManifest>(File.ReadAllText(fullPath), JsonOptions);
        if (snapshot is not null)
        {
            snapshot.Snapshot = fullPath;
        }

        return snapshot;
    }

    private SnapshotManifest? LoadSnapshot(string? requestSnapshotPath, string? analysisSnapshotPath)
    {
        return LoadSnapshot(string.IsNullOrWhiteSpace(requestSnapshotPath) ? analysisSnapshotPath : requestSnapshotPath);
    }

    private static VisionAnalysis LoadAnalysis(string analysisPath)
    {
        var json = File.ReadAllText(analysisPath);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var messages = JsonSerializer.Deserialize<IReadOnlyList<VisionMessage>>(json, JsonOptions) ?? [];
            return new VisionAnalysis { Messages = messages };
        }

        return JsonSerializer.Deserialize<VisionAnalysis>(json, JsonOptions) ?? new VisionAnalysis();
    }

    private IEnumerable<VisionMessage> NormalizeMessages(VisionAnalysis analysis, int? maxItems)
    {
        var messages = analysis.Messages
            .Concat(analysis.CopyPoints.Select(ToMessage));
        var normalized = messages
            .Where(message => message.Bbox is not null || !string.IsNullOrWhiteSpace(message.Text))
            .OrderBy(message => message.Bbox?.Y ?? int.MaxValue)
            .ThenBy(message => message.Bbox?.X ?? int.MaxValue)
            .Select((message, index) => message with
            {
                Index = index + 1,
                Role = NormalizeRole(message.Role),
                Type = NormalizeMessageType(message.Type)
            });

        return maxItems is > 0 ? normalized.Take(maxItems.Value) : normalized;
    }

    private static VisionMessage ToMessage(VisionCopyPoint point)
    {
        var x = point.Point?.X ?? point.X;
        var y = point.Point?.Y ?? point.Y;
        return new VisionMessage
        {
            Speaker = point.Speaker,
            Role = point.Role,
            Type = point.Type,
            Text = point.Text,
            Bbox = new VisionBoundingBox
            {
                X = x,
                Y = y,
                W = 1,
                H = 1
            },
            Confidence = point.Confidence
        };
    }

    private bool TryCopyImageMessage(
        VisionMessage message,
        Rectangle screenRegion,
        IntPtr targetWindow,
        out Image? copiedImage,
        out string error)
    {
        copiedImage = null;
        if (message.Bbox is null || message.Bbox.EffectiveW <= 0 || message.Bbox.EffectiveH <= 0)
        {
            error = "missing_bbox";
            return false;
        }

        if (message.Bbox.X < 0
            || message.Bbox.Y < 0
            || message.Bbox.X + message.Bbox.EffectiveW > screenRegion.Width
            || message.Bbox.Y + message.Bbox.EffectiveH > screenRegion.Height)
        {
            error = "bbox_outside_screenshot";
            return false;
        }

        if (targetWindow == IntPtr.Zero || !NativeMethods.GetWindowRect(targetWindow, out _))
        {
            error = "target_window_not_found";
            return false;
        }

        var imageRect = new Rectangle(
            screenRegion.X + message.Bbox.X,
            screenRegion.Y + message.Bbox.Y,
            message.Bbox.EffectiveW,
            message.Bbox.EffectiveH);
        var centerX = imageRect.X + (imageRect.Width / 2);
        var centerY = imageRect.Y + (imageRect.Height / 2);
        EnsureForeground(targetWindow);

        var anchors = new[]
        {
            new Point(Math.Max(imageRect.Left, imageRect.Right - 8), centerY),
            new Point(Math.Min(imageRect.Right, imageRect.Left + 8), centerY),
            new Point(centerX, Math.Max(imageRect.Top, imageRect.Bottom - 8))
        };

        foreach (var anchor in anchors.Distinct())
        {
            var copyClicks = new[]
            {
                new Point(anchor.X + _options.ContextMenuCopyClickOffsetX, anchor.Y + _options.ContextMenuCopyClickOffsetY),
                new Point(anchor.X + _options.ContextMenuCopyClickOffsetX, anchor.Y - 46),
                new Point(anchor.X - 90, anchor.Y + _options.ContextMenuCopyClickOffsetY),
                new Point(anchor.X - 90, anchor.Y - 46),
                new Point(anchor.X, imageRect.Bottom + 34)
            };

            foreach (var copyClick in copyClicks.Distinct())
            {
                if (IsInsideInflated(imageRect, copyClick, 6))
                {
                    continue;
                }

                WindowsClipboard.Clear();
                Thread.Sleep(_options.InteractionDelayMs);

                _ = ContextMenuCopyClicker.RightClickAndClickCopy(
                    anchor,
                    [copyClick],
                    _options.InteractionDelayMs,
                    imageRect);
                Thread.Sleep(_options.VisibleExportImageCopyWaitMs);

                var copied = WindowsClipboard.Capture();

                if (copied.Image is null)
                {
                    copied.Dispose();
                    DismissContextMenuSafely(screenRegion, imageRect);
                    Thread.Sleep(_options.InteractionDelayMs);
                    continue;
                }

                copiedImage = copied.Image;
                error = string.Empty;
                return true;
            }
        }

        error = "clipboard_has_no_image_after_copy";
        return false;
    }

    private string? TryCopyTextMessage(
        VisionMessage message,
        Rectangle screenRegion,
        IntPtr targetWindow,
        out string error)
    {
        if (message.Bbox is null || message.Bbox.EffectiveW <= 0 || message.Bbox.EffectiveH <= 0)
        {
            error = "missing_bbox";
            return null;
        }

        if (message.Bbox.X < 0
            || message.Bbox.Y < 0
            || message.Bbox.X + message.Bbox.EffectiveW > screenRegion.Width
            || message.Bbox.Y + message.Bbox.EffectiveH > screenRegion.Height)
        {
            error = "bbox_outside_screenshot";
            return null;
        }

        if (targetWindow == IntPtr.Zero || !NativeMethods.GetWindowRect(targetWindow, out _))
        {
            error = "target_window_not_found";
            return null;
        }

        var textRect = new Rectangle(
            screenRegion.X + message.Bbox.X,
            screenRegion.Y + message.Bbox.Y,
            message.Bbox.EffectiveW,
            message.Bbox.EffectiveH);
        var center = new Point(textRect.X + (textRect.Width / 2), textRect.Y + (textRect.Height / 2));
        EnsureForeground(targetWindow);

        var copyClicks = new[]
        {
            new Point(center.X + _options.ContextMenuCopyClickOffsetX, center.Y + _options.ContextMenuCopyClickOffsetY),
            new Point(center.X + _options.ContextMenuCopyClickOffsetX, center.Y - 46),
            new Point(center.X - 90, center.Y + _options.ContextMenuCopyClickOffsetY),
            new Point(center.X - 90, center.Y - 46),
            new Point(center.X, textRect.Bottom + 34)
        };

        foreach (var copyClick in copyClicks.Distinct())
        {
            if (IsInsideInflated(textRect, copyClick, 6))
            {
                continue;
            }

            WindowsClipboard.Clear();
            Thread.Sleep(_options.InteractionDelayMs);

            _ = ContextMenuCopyClicker.RightClickAndClickCopy(
                center,
                [copyClick],
                _options.InteractionDelayMs,
                textRect);
            Thread.Sleep(_options.VisibleExportImageCopyWaitMs);

            using var copied = WindowsClipboard.Capture();
            var text = NormalizeCopiedText(copied.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                DismissContextMenuSafely(screenRegion, textRect);
                Thread.Sleep(_options.InteractionDelayMs);
                continue;
            }

            error = string.Empty;
            return text;
        }

        error = "clipboard_has_no_text_after_copy";
        return null;
    }

    private static void DismissContextMenuSafely(Rectangle popupRegion, Rectangle avoidRect)
    {
        var point = GetPopupScrollPoint(popupRegion);
        if (IsInsideInflated(avoidRect, point, 8))
        {
            point = new Point(popupRegion.Right - 32, popupRegion.Top + 64);
        }

        if (IsInsideInflated(avoidRect, point, 8))
        {
            point = new Point(popupRegion.Left + 32, popupRegion.Bottom - 32);
        }

        MouseInputDriver.Click(point.X, point.Y);
    }

    private static bool IsInsideInflated(Rectangle rectangle, Point point, int inflate)
    {
        var inflated = rectangle;
        inflated.Inflate(inflate, inflate);
        return inflated.Contains(point);
    }

    private void EnsureForeground(IntPtr hWnd)
    {
        if (NativeMethods.GetForegroundWindow() == hWnd)
        {
            return;
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);
    }

    private IntPtr ResolvePopupWindow(string? windowHandle)
    {
        if (!string.IsNullOrWhiteSpace(windowHandle) && TryParseHandle(windowHandle, out var hWnd))
        {
            if (NativeMethods.GetWindowRect(hWnd, out _))
            {
                return hWnd;
            }
        }

        return FindPopupWindow(new HashSet<IntPtr>());
    }

    private IntPtr ResolveTargetWindow(string? preferredHandle, IntPtr fallback)
    {
        var resolved = ResolvePopupWindow(preferredHandle);
        if (resolved != IntPtr.Zero)
        {
            return resolved;
        }

        if (fallback != IntPtr.Zero && NativeMethods.GetWindowRect(fallback, out _))
        {
            return fallback;
        }

        return IntPtr.Zero;
    }

    private IntPtr FindPopupWindow(ISet<IntPtr> before)
    {
        foreach (var window in EnumerateVisibleWindows())
        {
            if (!window.Title.Contains(_options.MergedPopupTitleKeyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (before.Count == 0 || !before.Contains(window.Handle))
            {
                return window.Handle;
            }
        }

        foreach (var window in EnumerateVisibleWindows())
        {
            if (window.Title.Contains(_options.MergedPopupTitleKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return window.Handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<WindowInfo> EnumerateVisibleWindows()
    {
        var windows = new List<WindowInfo>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            windows.Add(new WindowInfo(hWnd, GetTitle(hWnd)));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private Rectangle CalculateChatRegion(NativeMethods.Rect windowRect)
    {
        var width = windowRect.Right - windowRect.Left;
        var height = windowRect.Bottom - windowRect.Top;
        return new Rectangle(
            windowRect.Left + _options.ChatRegionLeftOffset,
            windowRect.Top + _options.ChatRegionTopOffset,
            width - _options.ChatRegionLeftOffset - _options.ChatRegionRightOffset,
            height - _options.ChatRegionTopOffset - _options.ChatRegionBottomOffset);
    }

    private static Bitmap Capture(Rectangle region)
    {
        var bitmap = new Bitmap(region.Width, region.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size);
        return bitmap;
    }

    private static Rectangle ToRectangle(NativeMethods.Rect rect)
    {
        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static Point GetPopupScrollPoint(Rectangle region)
    {
        return new Point(
            region.Right - Math.Min(64, Math.Max(24, region.Width / 9)),
            region.Bottom - Math.Min(72, Math.Max(36, region.Height / 10)));
    }

    private static string? GetStringDataProperty(object? data, string propertyName)
    {
        if (data is null)
        {
            return null;
        }

        var property = data.GetType().GetProperty(propertyName);
        return property?.GetValue(data)?.ToString();
    }

    private static int CalculateDefaultPopupScrollNotches(Rectangle region)
    {
        if (region.Height >= 720)
        {
            return 8;
        }

        if (region.Height >= 560)
        {
            return 6;
        }

        return 4;
    }

    private static int PixelsToWheelNotches(int pixels)
    {
        if (pixels == 0)
        {
            return 0;
        }

        return Math.Sign(pixels) * Math.Max(1, (int)Math.Ceiling(Math.Abs(pixels) / 10.0));
    }

    private string ResolveSpeaker(VisionMessage message)
    {
        return message.Role switch
        {
            "self" => string.IsNullOrWhiteSpace(_markdownOptions.SelfSpeakerName)
                ? "我"
                : _markdownOptions.SelfSpeakerName.Trim(),
            "system" => "系统",
            _ => string.IsNullOrWhiteSpace(message.Speaker) ? "对方" : message.Speaker.Trim()
        };
    }

    private static bool IsImageMessage(VisionMessage message)
    {
        return message.Type.Equals("image", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeRole(string? value)
    {
        return NormalizeToken(value) switch
        {
            "me" or "mine" or "right" => "self",
            "left" => "other",
            var role => role
        };
    }

    private static string NormalizeMessageType(string? value)
    {
        return NormalizeToken(value) switch
        {
            "picture" or "photo" => "image",
            var type => type
        };
    }

    private static string NormalizeTextKey(string speaker, string role, string type, string text)
    {
        var normalizedText = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return $"{speaker.Trim()}|{NormalizeRole(role)}|{NormalizeMessageType(type)}|{normalizedText}";
    }

    private static string NormalizeCopiedText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var buffer = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string FormatHandle(IntPtr hWnd)
    {
        return "0x" + hWnd.ToInt64().ToString("X");
    }

    private static bool TryParseHandle(string value, out IntPtr hWnd)
    {
        hWnd = IntPtr.Zero;
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex)
                && (hWnd = new IntPtr(hex)) != IntPtr.Zero;
        }

        return long.TryParse(trimmed, out var number)
            && (hWnd = new IntPtr(number)) != IntPtr.Zero;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string HashImage(Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static ExportState LoadState(string statePath)
    {
        if (!File.Exists(statePath))
        {
            return new ExportState();
        }

        try
        {
            return JsonSerializer.Deserialize<ExportState>(File.ReadAllText(statePath), JsonOptions) ?? new ExportState();
        }
        catch
        {
            return new ExportState();
        }
    }

    private static void SaveState(string statePath, ExportState state)
    {
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions), Encoding.UTF8);
    }

    private static void SaveDebugImage(Bitmap screenshot, IReadOnlyList<VisionMessage> messages, string path)
    {
        using var debug = new Bitmap(screenshot);
        using var graphics = Graphics.FromImage(debug);
        using var textPen = new Pen(Color.DeepSkyBlue, 2);
        using var imagePen = new Pen(Color.OrangeRed, 2);
        using var font = new Font(FontFamily.GenericSansSerif, 12);
        foreach (var message in messages)
        {
            if (message.Bbox is null)
            {
                continue;
            }

            var rectangle = new Rectangle(message.Bbox.X, message.Bbox.Y, message.Bbox.EffectiveW, message.Bbox.EffectiveH);
            var pen = IsImageMessage(message) ? imagePen : textPen;
            graphics.DrawRectangle(pen, rectangle);
            graphics.DrawString(
                $"{message.Index}:{message.Type}",
                font,
                Brushes.Red,
                rectangle.X,
                Math.Max(0, rectangle.Y - 16));
        }

        debug.Save(path, ImageFormat.Png);
    }

    private sealed record WindowInfo(IntPtr Handle, string Title);

    private sealed record SnapshotRegion(int X, int Y, int Width, int Height)
    {
        public Rectangle ToRectangle()
        {
            return new Rectangle(X, Y, Width, Height);
        }
    }

    private sealed record SnapshotManifest
    {
        public string Output { get; init; } = string.Empty;

        public string Screenshot { get; init; } = string.Empty;

        public SnapshotRegion Region { get; init; } = new(0, 0, 0, 0);

        public string? WindowKind { get; init; }

        public string? WindowHandle { get; init; }

        public string? ScreenshotHash { get; init; }

        public string? SuggestedAnalysis { get; init; }

        public string? Index { get; init; }

        public string? Snapshot { get; set; }
    }

    private sealed record VisionAnalysis
    {
        public string? Output { get; init; }

        public string? Snapshot { get; init; }

        public string? Screenshot { get; init; }

        public string? WindowHandle { get; init; }

        public SnapshotRegion? Region { get; init; }

        public IReadOnlyList<VisionMessage> Messages { get; init; } = [];

        public IReadOnlyList<VisionCopyPoint> CopyPoints { get; init; } = [];
    }

    private sealed record VisionCopyPoint
    {
        public string? Speaker { get; init; }

        public string Role { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string? Text { get; init; }

        public int X { get; init; }

        public int Y { get; init; }

        public VisionPoint? Point { get; init; }

        public double? Confidence { get; init; }
    }

    private sealed record VisionPoint
    {
        public int X { get; init; }

        public int Y { get; init; }
    }

    private sealed record VisionMessage
    {
        public int Index { get; init; }

        public string? Speaker { get; init; }

        public string Role { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string? Text { get; init; }

        public VisionBoundingBox? Bbox { get; init; }

        public double? Confidence { get; init; }
    }

    private sealed record VisionBoundingBox
    {
        public int X { get; init; }

        public int Y { get; init; }

        public int W { get; init; }

        public int H { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }

        public int EffectiveW => W > 0 ? W : Width;

        public int EffectiveH => H > 0 ? H : Height;
    }

    private sealed record ExportState
    {
        public HashSet<string> TextKeys { get; init; } = [];

        public HashSet<string> ImageHashes { get; init; } = [];
    }
}
