using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using WxBridge.Core;

namespace WxBridge.Windows;

[SupportedOSPlatform("windows6.1")]
public sealed class VisibleChatExportService : IVisibleChatExportService
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

    public VisibleChatExportService(WxBridgeOptions options)
    {
        _options = options.WeChat;
        _markdownOptions = options.Markdown;
        _windowLocator = new WeChatWindowLocator(_options);
    }

    public OperationResult ExportVisible(ExportVisibleChatRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("messages.export_visible", "windows_required");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return OperationResult.Failure("messages.export_visible", "output_path_is_empty");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure("messages.export_visible", "wechat_window_not_found");
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);

        if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
        {
            return OperationResult.Failure("messages.export_visible", "get_window_rect_failed");
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return OperationResult.Failure("messages.export_visible", "invalid_output_path", new { outputPath });
        }

        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outputPath) + "_assets");
        var avatarsDir = Path.Combine(assetsDir, "avatars");
        Directory.CreateDirectory(avatarsDir);

        var region = CalculateChatRegion(windowRect);
        if (region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure("messages.export_visible", "invalid_chat_region", new { region });
        }

        using var screenshot = Capture(region);
        var screenshotPath = Path.Combine(assetsDir, "visible-chat.png");
        screenshot.Save(screenshotPath, ImageFormat.Png);

        var leftInset = request.LeftInset ?? _options.VisibleExportLeftInset;
        var rightInset = request.RightInset ?? _options.VisibleExportRightInset;
        var scanStep = request.ScanStep ?? _options.VisibleExportScanStep;
        var candidates = DetectAvatarCandidates(screenshot, leftInset, rightInset, scanStep);
        if (request.MaxItems is > 0)
        {
            candidates = candidates.Take(request.MaxItems.Value).ToList();
        }

        var entries = BuildEntries(candidates, screenshot, region, windowRect, hWnd, request.ResolveNames, request.CopyContent);
        SaveMarkdown(outputPath, avatarsDir, entries, screenshot, region);

        var debugPath = Path.Combine(assetsDir, "visible-chat-debug.png");
        SaveDebugImage(screenshot, candidates, debugPath);

        return OperationResult.Success(
            "messages.export_visible",
            new
            {
                output = outputPath,
                screenshot = screenshotPath,
                debug = debugPath,
                avatars = avatarsDir,
                region = new { region.X, region.Y, region.Width, region.Height },
                scan = new { leftInset, rightInset, scanStep },
                count = candidates.Count,
                resolveNames = request.ResolveNames,
                copyContent = request.CopyContent,
                maxItems = request.MaxItems,
                candidates = entries.Select(c => new
                {
                    c.Candidate.Index,
                    side = c.Candidate.Side,
                    name = c.Name,
                    content = c.Content,
                    local = new { c.Candidate.Bounds.X, c.Candidate.Bounds.Y, c.Candidate.Bounds.Width, c.Candidate.Bounds.Height },
                    screen = new
                    {
                        x = region.X + c.Candidate.Bounds.X,
                        y = region.Y + c.Candidate.Bounds.Y,
                        c.Candidate.Bounds.Width,
                        c.Candidate.Bounds.Height
                    },
                    c.Candidate.Score
                })
            });
    }

    public OperationResult SnapshotVisible(SnapshotVisibleChatRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("messages.snapshot_visible", "windows_required");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return OperationResult.Failure("messages.snapshot_visible", "output_path_is_empty");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure("messages.snapshot_visible", "wechat_window_not_found");
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);

        if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
        {
            return OperationResult.Failure("messages.snapshot_visible", "get_window_rect_failed");
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return OperationResult.Failure("messages.snapshot_visible", "invalid_output_path", new { outputPath });
        }

        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outputPath) + "_assets");
        Directory.CreateDirectory(assetsDir);

        var region = CalculateChatRegion(windowRect);
        if (region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure("messages.snapshot_visible", "invalid_chat_region", new { region });
        }

        using var screenshot = Capture(region);
        var screenshotPath = Path.Combine(assetsDir, "visible-chat-snapshot.png");
        screenshot.Save(screenshotPath, ImageFormat.Png);

        var snapshotPath = Path.Combine(assetsDir, "visible-chat-snapshot.json");
        var suggestedAnalysisPath = Path.Combine(assetsDir, "visible-chat-analysis.json");
        var snapshot = new VisibleChatSnapshot(
            outputPath,
            screenshotPath,
            new VisionScreenRegion(region.X, region.Y, region.Width, region.Height));
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions), Encoding.UTF8);

        return OperationResult.Success(
            "messages.snapshot_visible",
            new
            {
                output = outputPath,
                screenshot = screenshotPath,
                snapshot = snapshotPath,
                suggestedAnalysis = suggestedAnalysisPath,
                region = new { region.X, region.Y, region.Width, region.Height },
                analysisSchema = new
                {
                    output = outputPath,
                    snapshot = snapshotPath,
                    region = new { region.X, region.Y, region.Width, region.Height },
                    copyPoints = new[]
                    {
                        new
                        {
                            speaker = "sender name or empty",
                            role = "self|other|system",
                            type = "text|image|other",
                            x = 0,
                            y = 0
                        }
                    }
                }
            });
    }

    public OperationResult ApplyVisibleAnalysis(ApplyVisibleChatAnalysisRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("messages.apply_visible_analysis", "windows_required");
        }

        if (string.IsNullOrWhiteSpace(request.AnalysisPath))
        {
            return OperationResult.Failure("messages.apply_visible_analysis", "analysis_path_is_empty");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure("messages.apply_visible_analysis", "wechat_window_not_found");
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);

        var analysisPath = Path.GetFullPath(request.AnalysisPath);
        if (!File.Exists(analysisPath))
        {
            return OperationResult.Failure("messages.apply_visible_analysis", "analysis_not_found", new { analysis = analysisPath });
        }

        var analysis = LoadVisionChatAnalysis(analysisPath);
        var snapshot = LoadSnapshot(request.SnapshotPath, analysis.Snapshot);
        var outputPath = Path.GetFullPath(request.OutputPath ?? analysis.Output ?? snapshot?.Output ?? string.Empty);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return OperationResult.Failure(
                "messages.apply_visible_analysis",
                "missing_output_path",
                new { option = "--output or analysis.output or snapshot.output" });
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return OperationResult.Failure("messages.apply_visible_analysis", "invalid_output_path", new { outputPath });
        }

        Directory.CreateDirectory(outputDir);
        var assetsDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outputPath) + "_assets");
        Directory.CreateDirectory(assetsDir);

        var region = analysis.Region ?? snapshot?.Region;
        if (region is null || region.Width <= 0 || region.Height <= 0)
        {
            return OperationResult.Failure(
                "messages.apply_visible_analysis",
                "missing_region",
                new { required = "analysis.region or snapshot.region" });
        }

        var screenshotPath = analysis.Screenshot ?? snapshot?.Screenshot;
        var messages = NormalizeVisionMessages(analysis, request.MaxItems).ToList();
        var writer = new MarkdownCaptureWriter(outputPath);
        var copiedImages = 0;
        var failedImages = 0;
        var copiedTexts = 0;
        var failedTexts = 0;
        var fallbackTexts = 0;
        var writtenTexts = 0;
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
                if (!speaker.Equals(currentSpeaker, StringComparison.Ordinal))
                {
                    writer.SetSpeaker(speaker);
                    currentSpeaker = speaker;
                }

                if (IsImageMessage(message))
                {
                    if (TryCopyImageMessage(message, region.ToRectangle(), hWnd, writer, out var error))
                    {
                        copiedImages++;
                    }
                    else
                    {
                        failedImages++;
                        writer.AppendText($"> [图片复制失败，需要人工补充：{error}]");
                    }

                    continue;
                }

                var copiedText = TryCopyTextMessage(message, region.ToRectangle(), hWnd, out _);
                var text = NormalizeCopiedText(copiedText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    copiedTexts++;
                    writer.AppendText(text);
                    writtenTexts++;
                    continue;
                }

                failedTexts++;
                text = NormalizeCopiedText(message.Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    fallbackTexts++;
                    writer.AppendText(text);
                    writtenTexts++;
                }
            }
        }
        finally
        {
            WindowsClipboard.Restore(originalClipboard);
        }

        string? debugPath = null;
        if (!string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath))
        {
            debugPath = Path.Combine(assetsDir, "visible-chat-analysis-debug.png");
            using var screenshot = new Bitmap(screenshotPath);
            SaveVisionDebugImage(screenshot, messages, debugPath);
        }

        return OperationResult.Success(
            "messages.apply_visible_analysis",
            new
            {
                output = outputPath,
                screenshot = screenshotPath,
                debug = debugPath,
                analysis = analysisPath,
                region,
                count = messages.Count,
                writtenTexts,
                copiedTexts,
                failedTexts,
                fallbackTexts,
                copiedImages,
                failedImages,
                messages = messages.Select(message => new
                {
                    message.Index,
                    speaker = ResolveSpeaker(message),
                    message.Role,
                    message.Type,
                    message.Text,
                    bbox = message.Bbox,
                    message.Confidence
                })
            });
    }

    private static VisionChatAnalysis LoadVisionChatAnalysis(string analysisPath)
    {
        var json = File.ReadAllText(analysisPath);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var messages = JsonSerializer.Deserialize<IReadOnlyList<VisionChatMessage>>(json, JsonOptions) ?? [];
            return new VisionChatAnalysis { Messages = messages };
        }

        return JsonSerializer.Deserialize<VisionChatAnalysis>(json, JsonOptions)
            ?? new VisionChatAnalysis();
    }

    private static VisibleChatSnapshot? LoadSnapshot(string? requestSnapshotPath, string? analysisSnapshotPath)
    {
        var snapshotPath = string.IsNullOrWhiteSpace(requestSnapshotPath)
            ? analysisSnapshotPath
            : requestSnapshotPath;
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(snapshotPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<VisibleChatSnapshot>(json, JsonOptions);
    }

    private IEnumerable<VisionChatMessage> NormalizeVisionMessages(VisionChatAnalysis analysis, int? maxItems)
    {
        var messages = analysis.Messages.Concat(analysis.CopyPoints.Select(ToMessage));
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

        if (maxItems is > 0)
        {
            normalized = normalized.Take(maxItems.Value);
        }

        return normalized;
    }

    private static VisionChatMessage ToMessage(VisionCopyPoint point)
    {
        var x = point.Point?.X ?? point.X;
        var y = point.Point?.Y ?? point.Y;
        return new VisionChatMessage
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

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
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

    private string ResolveSpeaker(VisionChatMessage message)
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

    private static bool IsImageMessage(VisionChatMessage message)
    {
        return message.Type.Equals("image", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryCopyImageMessage(
        VisionChatMessage message,
        Rectangle screenRegion,
        IntPtr hWnd,
        MarkdownCaptureWriter writer,
        out string error)
    {
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

        var centerX = screenRegion.X + message.Bbox.X + (message.Bbox.EffectiveW / 2);
        var centerY = screenRegion.Y + message.Bbox.Y + (message.Bbox.EffectiveH / 2);
        EnsureWeChatForeground(hWnd);

        var copyClicks = new[]
        {
            new Point(centerX + _options.ContextMenuCopyClickOffsetX, centerY + _options.ContextMenuCopyClickOffsetY),
            new Point(centerX + _options.ContextMenuCopyClickOffsetX, centerY - 46),
            new Point(centerX + _options.ContextMenuCopyClickOffsetX, centerY - 82),
            new Point(centerX - 70, centerY - 46)
        };

        foreach (var copyClick in copyClicks.Distinct())
        {
            WindowsClipboard.Clear();
            Thread.Sleep(_options.InteractionDelayMs);

            _ = ContextMenuCopyClicker.RightClickAndClickCopy(
                new Point(centerX, centerY),
                [copyClick],
                _options.InteractionDelayMs);
            Thread.Sleep(_options.VisibleExportImageCopyWaitMs);

            using var copied = WindowsClipboard.Capture();
            KeyboardInputDriver.Escape();
            Thread.Sleep(_options.InteractionDelayMs);

            if (copied.Image is null)
            {
                continue;
            }

            writer.AppendImage(copied.Image);
            error = string.Empty;
            return true;
        }

        error = "clipboard_has_no_image_after_copy";
        return false;
    }

    private string? TryCopyTextMessage(
        VisionChatMessage message,
        Rectangle screenRegion,
        IntPtr hWnd,
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

        var textRect = new Rectangle(
            screenRegion.X + message.Bbox.X,
            screenRegion.Y + message.Bbox.Y,
            message.Bbox.EffectiveW,
            message.Bbox.EffectiveH);
        var center = new Point(textRect.X + (textRect.Width / 2), textRect.Y + (textRect.Height / 2));
        EnsureWeChatForeground(hWnd);

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
                KeyboardInputDriver.Escape();
                Thread.Sleep(_options.InteractionDelayMs);
                continue;
            }

            error = string.Empty;
            return text;
        }

        error = "clipboard_has_no_text_after_copy";
        return null;
    }

    private static bool IsInsideInflated(Rectangle rectangle, Point point, int padding)
    {
        var inflated = rectangle;
        inflated.Inflate(padding, padding);
        return inflated.Contains(point);
    }

    private static string NormalizeCopiedText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private static void SaveVisionDebugImage(Bitmap screenshot, IReadOnlyList<VisionChatMessage> messages, string path)
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

    private List<AvatarCandidate> DetectAvatarCandidates(Bitmap bitmap, int leftInset, int rightInset, int scanStep)
    {
        var raw = new List<AvatarCandidate>();
        ScanLeft(bitmap, leftInset, scanStep, raw);
        ScanRight(bitmap, rightInset, scanStep, raw);

        return raw
            .OrderBy(c => c.CenterY)
            .ThenBy(c => c.Side)
            .GroupBy(c => c.Side)
            .SelectMany(group => MergeCloseCandidates(group.OrderBy(c => c.CenterY)))
            .Where(candidate => IsSafeCandidate(candidate, bitmap.Height))
            .OrderBy(c => c.CenterY)
            .Select((candidate, index) => candidate with { Index = index + 1 })
            .ToList();
    }

    private void ScanLeft(Bitmap bitmap, int leftInset, int scanStep, List<AvatarCandidate> raw)
    {
        var size = _options.VisibleExportAvatarSize;
        var maxX = Math.Min(_options.VisibleExportAvatarBandWidth, bitmap.Width) - size;
        for (var y = 0; y <= bitmap.Height - size; y += scanStep)
        {
            for (var x = Math.Max(0, leftInset); x <= maxX; x += scanStep)
            {
                var score = ScoreWindow(bitmap, x, y, size);
                if (score >= MinScore(size))
                {
                    raw.Add(new AvatarCandidate(0, "left", new Rectangle(x, y, size, size), score));
                    break;
                }
            }
        }
    }

    private void ScanRight(Bitmap bitmap, int rightInset, int scanStep, List<AvatarCandidate> raw)
    {
        var size = _options.VisibleExportAvatarSize;
        var minX = Math.Max(0, bitmap.Width - _options.VisibleExportAvatarBandWidth);
        var startX = bitmap.Width - Math.Max(0, rightInset) - _options.VisibleExportRightGutterWidth - size;
        for (var y = 0; y <= bitmap.Height - size; y += scanStep)
        {
            for (var x = startX; x >= minX; x -= scanStep)
            {
                var score = ScoreWindow(bitmap, x, y, size);
                if (score >= MinScore(size))
                {
                    raw.Add(new AvatarCandidate(0, "right", new Rectangle(x, y, size, size), score));
                    break;
                }
            }
        }
    }

    private IEnumerable<AvatarCandidate> MergeCloseCandidates(IEnumerable<AvatarCandidate> candidates)
    {
        AvatarCandidate? best = null;
        foreach (var candidate in candidates)
        {
            if (best is null)
            {
                best = candidate;
                continue;
            }

            if (Math.Abs(candidate.CenterY - best.CenterY) <= _options.VisibleExportCandidateMinGap)
            {
                if (candidate.Score > best.Score)
                {
                    best = candidate;
                }

                continue;
            }

            yield return best;
            best = candidate;
        }

        if (best is not null)
        {
            yield return best;
        }
    }

    private bool IsSafeCandidate(AvatarCandidate candidate, int bitmapHeight)
    {
        return candidate.Bounds.Top >= _options.VisibleExportCandidateTopSafeMargin
            && candidate.Bounds.Bottom <= bitmapHeight - _options.VisibleExportCandidateBottomSafeMargin;
    }

    private static int ScoreWindow(Bitmap bitmap, int x, int y, int size)
    {
        var score = 0;
        for (var yy = y; yy < y + size; yy++)
        {
            for (var xx = x; xx < x + size; xx++)
            {
                if (IsForeground(bitmap.GetPixel(xx, yy)))
                {
                    score++;
                }
            }
        }

        return score;
    }

    private static int MinScore(int size)
    {
        return (int)(size * size * 0.12);
    }

    private static bool IsForeground(Color color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        var min = Math.Min(color.R, Math.Min(color.G, color.B));
        var lightNeutral = max >= 232 && max - min <= 24;
        return !lightNeutral;
    }

    private List<VisibleChatEntry> BuildEntries(
        IReadOnlyList<AvatarCandidate> candidates,
        Bitmap screenshot,
        Rectangle screenRegion,
        NativeMethods.Rect windowRect,
        IntPtr hWnd,
        bool resolveNames,
        bool copyContent)
    {
        var entries = candidates
            .Select(candidate => new VisibleChatEntry(candidate, null, null))
            .ToList();

        if (!resolveNames && !copyContent)
        {
            return entries;
        }

        FocusAndClearInput(hWnd, windowRect);

        for (var i = 0; i < entries.Count; i++)
        {
            var candidate = entries[i].Candidate;
            var name = resolveNames ? TryResolveName(candidate, screenRegion, hWnd, windowRect) : null;
            var content = copyContent ? TryCopyContent(candidate, screenRegion, hWnd) : null;
            entries[i] = entries[i] with { Name = name, Content = content };
        }

        FocusAndClearInput(hWnd, windowRect);
        return entries;
    }

    private string? TryResolveName(AvatarCandidate candidate, Rectangle screenRegion, IntPtr hWnd, NativeMethods.Rect windowRect)
    {
        EnsureWeChatForeground(hWnd);
        var avatarCenter = ToScreenPoint(candidate.Bounds, screenRegion);
        MouseInputDriver.RightClick(avatarCenter.X, avatarCenter.Y);
        Thread.Sleep(_options.InteractionDelayMs);

        var mentionX = avatarCenter.X + (candidate.Side == "left"
            ? _options.AvatarMentionClickLeftOffsetX
            : _options.AvatarMentionClickRightOffsetX);
        var mentionY = avatarCenter.Y + _options.AvatarMentionClickOffsetY;
        EnsureWeChatForeground(hWnd);
        MouseInputDriver.Click(mentionX, mentionY);
        Thread.Sleep(_options.InteractionDelayMs);

        FocusInput(hWnd, windowRect);
        KeyboardInputDriver.SelectAll();
        Thread.Sleep(_options.InteractionDelayMs);
        KeyboardInputDriver.Copy();
        Thread.Sleep(_options.InteractionDelayMs);

        var value = WindowsClipboard.GetText().Trim();
        KeyboardInputDriver.Backspace();
        Thread.Sleep(_options.InteractionDelayMs);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.TrimStart('@').Trim();
    }

    private string? TryCopyContent(AvatarCandidate candidate, Rectangle screenRegion, IntPtr hWnd)
    {
        EnsureWeChatForeground(hWnd);
        foreach (var offsetY in _options.MessageCopyClickOffsetYs)
        {
            WindowsClipboard.SetText(string.Empty);
            var contentPoint = GetContentPoint(candidate, screenRegion, offsetY);
            MouseInputDriver.RightClick(contentPoint.X, contentPoint.Y);
            Thread.Sleep(_options.InteractionDelayMs);

            MouseInputDriver.Click(
                contentPoint.X + _options.ContextMenuCopyClickOffsetX,
                contentPoint.Y + _options.ContextMenuCopyClickOffsetY);
            Thread.Sleep(_options.InteractionDelayMs);

            var value = WindowsClipboard.GetText().Trim();
            KeyboardInputDriver.Escape();
            Thread.Sleep(_options.InteractionDelayMs);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private void FocusAndClearInput(IntPtr hWnd, NativeMethods.Rect windowRect)
    {
        FocusInput(hWnd, windowRect);
        KeyboardInputDriver.SelectAll();
        Thread.Sleep(_options.InteractionDelayMs);
        KeyboardInputDriver.Backspace();
        Thread.Sleep(_options.InteractionDelayMs);
    }

    private void FocusInput(IntPtr hWnd, NativeMethods.Rect windowRect)
    {
        EnsureWeChatForeground(hWnd);
        var x = windowRect.Left + ((windowRect.Right - windowRect.Left) / 2);
        var y = windowRect.Bottom - _options.InputBoxClickYOffsetFromWindowBottom;
        MouseInputDriver.Click(x, y);
        Thread.Sleep(_options.InteractionDelayMs);
    }

    private void EnsureWeChatForeground(IntPtr hWnd)
    {
        if (NativeMethods.GetForegroundWindow() == hWnd)
        {
            return;
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hWnd);
        Thread.Sleep(_options.ActivateDelayMs);
    }

    private Point ToScreenPoint(Rectangle localBounds, Rectangle screenRegion)
    {
        return new Point(
            screenRegion.X + localBounds.X + (localBounds.Width / 2),
            screenRegion.Y + localBounds.Y + (localBounds.Height / 2));
    }

    private Point GetContentPoint(AvatarCandidate candidate, Rectangle screenRegion, int offsetY)
    {
        var avatarCenter = ToScreenPoint(candidate.Bounds, screenRegion);
        return new Point(
            avatarCenter.X + (candidate.Side == "left"
                ? _options.MessageCopyClickLeftOffsetX
                : _options.MessageCopyClickRightOffsetX),
            avatarCenter.Y + offsetY);
    }

    private static void SaveMarkdown(string outputPath, string avatarsDir, IReadOnlyList<VisibleChatEntry> entries, Bitmap screenshot, Rectangle screenRegion)
    {
        var outputDir = Path.GetDirectoryName(outputPath)!;
        var lines = new List<string>();
        lines.Add("<!-- Generated by WxBridge visible-region prototype. -->");
        lines.Add("");

        foreach (var entry in entries)
        {
            var candidate = entry.Candidate;
            var avatarPath = Path.Combine(avatarsDir, $"avatar-{candidate.Index:000}-{candidate.Side}.png");
            using (var avatar = screenshot.Clone(candidate.Bounds, screenshot.PixelFormat))
            {
                avatar.Save(avatarPath, ImageFormat.Png);
            }

            var relativeAvatarPath = Path.GetRelativePath(outputDir, avatarPath).Replace('\\', '/');
            lines.Add($"# {(string.IsNullOrWhiteSpace(entry.Name) ? $"![]({relativeAvatarPath})" : entry.Name)}");
            lines.Add("");
            if (!string.IsNullOrWhiteSpace(entry.Content))
            {
                lines.Add(entry.Content);
                lines.Add("");
            }

            lines.Add($"<!-- side: {candidate.Side}; screen: x={screenRegion.X + candidate.Bounds.X}, y={screenRegion.Y + candidate.Bounds.Y}, w={candidate.Bounds.Width}, h={candidate.Bounds.Height}; score: {candidate.Score} -->");
            lines.Add("");
        }

        File.WriteAllLines(outputPath, lines, Encoding.UTF8);
    }

    private static void SaveDebugImage(Bitmap screenshot, IReadOnlyList<AvatarCandidate> candidates, string path)
    {
        using var debug = new Bitmap(screenshot);
        using var graphics = Graphics.FromImage(debug);
        using var leftPen = new Pen(Color.DeepSkyBlue, 2);
        using var rightPen = new Pen(Color.OrangeRed, 2);
        using var font = new Font(FontFamily.GenericSansSerif, 12);
        foreach (var candidate in candidates)
        {
            var pen = candidate.Side == "left" ? leftPen : rightPen;
            graphics.DrawRectangle(pen, candidate.Bounds);
            graphics.DrawString(candidate.Index.ToString(), font, Brushes.Red, candidate.Bounds.X, Math.Max(0, candidate.Bounds.Y - 16));
        }

        debug.Save(path, ImageFormat.Png);
    }

    private sealed record AvatarCandidate(int Index, string Side, Rectangle Bounds, int Score)
    {
        public int CenterY => Bounds.Y + (Bounds.Height / 2);
    }

    private sealed record VisibleChatEntry(AvatarCandidate Candidate, string? Name, string? Content);

    private sealed record VisibleChatSnapshot(
        string Output,
        string Screenshot,
        VisionScreenRegion Region);

    private sealed record VisionChatAnalysis
    {
        public string? Output { get; init; }

        public string? Snapshot { get; init; }

        public string? Screenshot { get; init; }

        public VisionScreenRegion? Region { get; init; }

        public IReadOnlyList<VisionChatMessage> Messages { get; init; } = [];

        public IReadOnlyList<VisionCopyPoint> CopyPoints { get; init; } = [];
    }

    private sealed record VisionScreenRegion(int X, int Y, int Width, int Height)
    {
        public Rectangle ToRectangle()
        {
            return new Rectangle(X, Y, Width, Height);
        }
    }

    private sealed record VisionChatMessage
    {
        public int Index { get; init; }

        public string? Speaker { get; init; }

        public string Role { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string? Text { get; init; }

        public VisionBoundingBox? Bbox { get; init; }

        public double? Confidence { get; init; }
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
}
