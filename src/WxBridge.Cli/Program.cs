using System.Text.Json;
using System.Diagnostics;
using System.Runtime.Versioning;
using WxBridge.Core;
using WxBridge.Windows;

if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
{
    Console.Error.WriteLine("WxBridge requires Windows.");
    return 1;
}

return CliApp.Run(args);

[SupportedOSPlatform("windows6.1")]
internal static class CliApp
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Run(string[] args)
    {
        EnableBackgroundLogging(args);
        var options = LoadOptions();
        var sessions = new CoordinateChatSessionService(options);
        var messages = new ClipboardMessageService(options);
        var visibleExports = new VisibleChatExportService(options);
        var merged = new MergedChatRecordService(options);
        var capture = new CaptureService(options);
        var result = Dispatch(args, options, sessions, messages, visibleExports, merged, capture);
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return result.Ok ? 0 : 1;
    }

    private static OperationResult Dispatch(
        string[] args,
        WxBridgeOptions options,
        IChatSessionService sessions,
        IMessageService messages,
        IVisibleChatExportService visibleExports,
        IMergedChatRecordService merged,
        ICaptureService capture)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            return OperationResult.Success("help", new
            {
                commands = new[]
                {
                    "wxbridge status",
                    "wxbridge sessions list",
                    "wxbridge sessions inspect --view raw --max-depth 6 --limit 300",
                    "wxbridge sessions switch --index 1",
                    "wxbridge sessions switch --name \"contact-a\"",
                    "wxbridge sessions open --name \"contact-a\"",
                    "wxbridge messages send-text --text \"hello\"",
                    "wxbridge messages send-file --path \"<path-to-file>\"",
                    "wxbridge messages send-clipboard",
                    "wxbridge messages send-text-to --index 1 --text \"hello\"",
                    "wxbridge messages send-file-to --index 1 --path \"<path-to-video>\"",
                    "wxbridge messages send-clipboard-to --index 1",
                    "wxbridge messages export-visible --output \"<captures-dir>\\\\chat.md\"",
                    "wxbridge messages export-visible --name \"manual-capture\"",
                    "wxbridge messages snapshot-visible --name \"visible-capture\"",
                    "wxbridge messages apply-visible-analysis --input \"<analysis-json>\"",
                    "wxbridge messages export-visible --output \"<captures-dir>\\\\chat.md\" --resolve-names --copy-content --max-items 1",
                    "wxbridge merged snapshot-entry --name \"merged-capture\"",
                    "wxbridge merged open-entry --snapshot \"<snapshot-json>\" --x 100 --y 100 --w 200 --h 80",
                    "wxbridge merged open-entry-and-snapshot --snapshot \"<snapshot-json>\" --x 100 --y 100 --w 200 --h 80 --name \"merged-capture\" --index 001",
                    "wxbridge merged snapshot-popup --name \"merged-capture\" --hwnd \"0x123456\" --index 001",
                    "wxbridge merged apply-popup-analysis --input \"<analysis-json>\"",
                    "wxbridge merged apply-scroll-snapshot --input \"<analysis-json>\" --name \"merged-capture\" --index 002",
                    "wxbridge merged apply-popup-analysis --input \"<analysis-json>\" --skip-images",
                    "wxbridge merged scroll-popup --hwnd \"0x123456\"",
                    "wxbridge merged scroll-popup --hwnd \"0x123456\" --pixels 420",
                    "wxbridge capture start --output \"<captures-dir>\\\\chat.md\"",
                    "wxbridge capture start --name \"manual-capture\"",
                    "wxbridge capture start --background",
                    "wxbridge capture stop",
                    "wxbridge capture status",
                    "wxbridge capture logs --tail 80",
                    "wxbridge config set-output-dir --path \"<captures-dir>\"",
                    "wxbridge config set-output-name --name \"manual-capture\"",
                    "wxbridge config set-self-speaker --name \"我\""
                }
            });
        }

        if (args[0] == "status")
        {
            return OperationResult.Success("status", new
            {
                app = "WxBridge",
                dotnet = Environment.Version.ToString(),
                os = Environment.OSVersion.ToString()
            });
        }

        if (args.Length >= 2 && args[0] == "config" && args[1] == "set-output-dir")
        {
            var path = ReadStringOption(args, "--path") ?? ReadStringOption(args, "--output-dir");
            if (string.IsNullOrWhiteSpace(path))
            {
                return OperationResult.Failure("config.set_output_dir", "missing_required_option", new { option = "--path" });
            }

            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(fullPath);
            options.Markdown.OutputDirectory = fullPath;
            var configPath = SaveOptions(options);

            return OperationResult.Success(
                "config.set_output_dir",
                new
                {
                    outputDirectory = fullPath,
                    config = configPath
                });
        }

        if (args.Length >= 2 && args[0] == "config" && args[1] == "set-output-name")
        {
            var name = ReadStringOption(args, "--name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return OperationResult.Failure("config.set_output_name", "missing_required_option", new { option = "--name" });
            }

            var markdownName = NormalizeMarkdownFileName(name);
            if (markdownName is null)
            {
                return OperationResult.Failure("config.set_output_name", "invalid_markdown_name", new { name });
            }

            options.Markdown.OutputName = markdownName;
            var configPath = SaveOptions(options);

            return OperationResult.Success(
                "config.set_output_name",
                new
                {
                    outputName = markdownName,
                    config = configPath
                });
        }

        if (args.Length >= 2 && args[0] == "config" && args[1] == "set-self-speaker")
        {
            var name = ReadStringOption(args, "--name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return OperationResult.Failure("config.set_self_speaker", "missing_required_option", new { option = "--name" });
            }

            options.Markdown.SelfSpeakerName = name.Trim();
            var configPath = SaveOptions(options);

            return OperationResult.Success(
                "config.set_self_speaker",
                new
                {
                    selfSpeakerName = options.Markdown.SelfSpeakerName,
                    config = configPath
                });
        }

        if (args.Length >= 2 && args[0] == "sessions" && args[1] == "list")
        {
            return sessions.ListSessions();
        }

        if (args.Length >= 2 && args[0] == "sessions" && args[1] == "inspect")
        {
            return sessions.InspectSessions(new InspectSessionsRequest(
                ReadIntOption(args, "--max-depth") ?? 6,
                ReadIntOption(args, "--limit") ?? 300,
                HasFlag(args, "--include-empty"),
                ReadStringOption(args, "--view") ?? "control"));
        }

        if (args.Length >= 2 && args[0] == "sessions" && args[1] == "switch")
        {
            var index = ReadIntOption(args, "--index");
            if (index is not null)
            {
                return sessions.SwitchSession(new SwitchSessionRequest(index.Value));
            }

            var query = ReadStringOption(args, "--name") ?? ReadStringOption(args, "--query");
            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchResult = sessions.SearchSession(new SearchSessionRequest(query));
                return searchResult.Ok
                    ? OperationResult.Success("sessions.switch", searchResult.Data)
                    : OperationResult.Failure("sessions.switch", searchResult.Error ?? "search_failed", searchResult.Data);
            }

            return OperationResult.Failure("sessions.switch", "missing_required_option", new { option = "--index or --name" });
        }

        if (args.Length >= 2 && args[0] == "sessions" && args[1] == "open")
        {
            var query = ReadStringOption(args, "--name") ?? ReadStringOption(args, "--query");
            if (string.IsNullOrWhiteSpace(query))
            {
                return OperationResult.Failure("sessions.open", "missing_required_option", new { option = "--name" });
            }

            var searchResult = sessions.SearchSession(new SearchSessionRequest(query));
            return searchResult.Ok
                ? OperationResult.Success("sessions.open", searchResult.Data)
                : OperationResult.Failure("sessions.open", searchResult.Error ?? "search_failed", searchResult.Data);
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "send-text")
        {
            var text = ReadStringOption(args, "--text");
            if (text is null)
            {
                return OperationResult.Failure("messages.send_text", "missing_required_option", new { option = "--text" });
            }

            return messages.SendText(new SendTextRequest(text));
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "send-file")
        {
            var path = ReadStringOption(args, "--path");
            if (path is null)
            {
                return OperationResult.Failure("messages.send_file", "missing_required_option", new { option = "--path" });
            }

            return messages.SendFile(new SendFileRequest(path));
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "send-clipboard")
        {
            return messages.SendClipboard();
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "send-text-to")
        {
            var switchResult = SwitchByIndex(args, sessions, "messages.send_text_to");
            if (!switchResult.Ok)
            {
                return switchResult;
            }

            var text = ReadStringOption(args, "--text");
            if (text is null)
            {
                return OperationResult.Failure("messages.send_text_to", "missing_required_option", new { option = "--text" });
            }

            var sendResult = messages.SendText(new SendTextRequest(text));
            return sendResult.Ok
                ? OperationResult.Success("messages.send_text_to", new { switchResult.Data, send = sendResult.Data })
                : sendResult;
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "send-file-to")
        {
            var switchResult = SwitchByIndex(args, sessions, "messages.send_file_to");
            if (!switchResult.Ok)
            {
                return switchResult;
            }

            var path = ReadStringOption(args, "--path");
            if (path is null)
            {
                return OperationResult.Failure("messages.send_file_to", "missing_required_option", new { option = "--path" });
            }

            var sendResult = messages.SendFile(new SendFileRequest(path));
            return sendResult.Ok
                ? OperationResult.Success("messages.send_file_to", new { switchResult.Data, send = sendResult.Data })
                : sendResult;
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "send-clipboard-to")
        {
            var switchResult = SwitchByIndex(args, sessions, "messages.send_clipboard_to");
            if (!switchResult.Ok)
            {
                return switchResult;
            }

            var sendResult = messages.SendClipboard();
            return sendResult.Ok
                ? OperationResult.Success("messages.send_clipboard_to", new { switchResult.Data, send = sendResult.Data })
                : sendResult;
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "export-visible")
        {
            var (output, error) = ResolveMarkdownOutputPath(args, options, "messages.export_visible");
            if (error is not null)
            {
                return error;
            }

            return visibleExports.ExportVisible(new ExportVisibleChatRequest(
                output!,
                ReadIntOption(args, "--left-inset"),
                ReadIntOption(args, "--right-inset"),
                ReadIntOption(args, "--scan-step"),
                HasFlag(args, "--resolve-names"),
                HasFlag(args, "--copy-content"),
                ReadIntOption(args, "--max-items")));
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "snapshot-visible")
        {
            var (output, error) = ResolveMarkdownOutputPath(args, options, "messages.snapshot_visible");
            if (error is not null)
            {
                return error;
            }

            return visibleExports.SnapshotVisible(new SnapshotVisibleChatRequest(output!));
        }

        if (args.Length >= 2 && args[0] == "messages" && args[1] == "apply-visible-analysis")
        {
            var input = ReadStringOption(args, "--input") ?? ReadStringOption(args, "--analysis");
            if (string.IsNullOrWhiteSpace(input))
            {
                return OperationResult.Failure(
                    "messages.apply_visible_analysis",
                    "missing_required_option",
                    new { option = "--input" });
            }

            string? output = null;
            if (HasOption(args, "--output") || HasOption(args, "--name") || HasOption(args, "--output-dir"))
            {
                var resolved = ResolveMarkdownOutputPath(args, options, "messages.apply_visible_analysis");
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                output = resolved.OutputPath;
            }

            return visibleExports.ApplyVisibleAnalysis(new ApplyVisibleChatAnalysisRequest(
                input,
                ReadStringOption(args, "--snapshot"),
                output,
                ReadIntOption(args, "--max-items")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "snapshot-entry")
        {
            var (output, error) = ResolveMarkdownOutputPath(args, options, "merged.snapshot_entry");
            if (error is not null)
            {
                return error;
            }

            return merged.SnapshotEntry(new MergedSnapshotEntryRequest(output!));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "open-entry")
        {
            var snapshot = ReadStringOption(args, "--snapshot");
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                return OperationResult.Failure("merged.open_entry", "missing_required_option", new { option = "--snapshot" });
            }

            var x = ReadIntOption(args, "--x");
            var y = ReadIntOption(args, "--y");
            var w = ReadIntOption(args, "--w") ?? ReadIntOption(args, "--width");
            var h = ReadIntOption(args, "--h") ?? ReadIntOption(args, "--height");
            if (x is null || y is null || w is null || h is null)
            {
                return OperationResult.Failure("merged.open_entry", "missing_required_option", new { option = "--x --y --w --h" });
            }

            return merged.OpenEntry(new MergedOpenEntryRequest(snapshot, x.Value, y.Value, w.Value, h.Value));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "open-entry-and-snapshot")
        {
            var snapshot = ReadStringOption(args, "--snapshot");
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                return OperationResult.Failure("merged.open_entry_and_snapshot", "missing_required_option", new { option = "--snapshot" });
            }

            var x = ReadIntOption(args, "--x");
            var y = ReadIntOption(args, "--y");
            var w = ReadIntOption(args, "--w") ?? ReadIntOption(args, "--width");
            var h = ReadIntOption(args, "--h") ?? ReadIntOption(args, "--height");
            if (x is null || y is null || w is null || h is null)
            {
                return OperationResult.Failure("merged.open_entry_and_snapshot", "missing_required_option", new { option = "--x --y --w --h" });
            }

            var (output, error) = ResolveMarkdownOutputPath(args, options, "merged.open_entry_and_snapshot");
            if (error is not null)
            {
                return error;
            }

            return merged.OpenEntryAndSnapshot(new MergedOpenEntryAndSnapshotRequest(
                snapshot,
                output!,
                x.Value,
                y.Value,
                w.Value,
                h.Value,
                ReadStringOption(args, "--index")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "snapshot-popup")
        {
            var (output, error) = ResolveMarkdownOutputPath(args, options, "merged.snapshot_popup");
            if (error is not null)
            {
                return error;
            }

            return merged.SnapshotPopup(new MergedSnapshotPopupRequest(
                output!,
                ReadStringOption(args, "--hwnd") ?? ReadStringOption(args, "--window-handle"),
                ReadStringOption(args, "--index")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "apply-popup-analysis")
        {
            var input = ReadStringOption(args, "--input") ?? ReadStringOption(args, "--analysis");
            if (string.IsNullOrWhiteSpace(input))
            {
                return OperationResult.Failure(
                    "merged.apply_popup_analysis",
                    "missing_required_option",
                    new { option = "--input" });
            }

            string? output = null;
            if (HasOption(args, "--output") || HasOption(args, "--name") || HasOption(args, "--output-dir"))
            {
                var resolved = ResolveMarkdownOutputPath(args, options, "merged.apply_popup_analysis");
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                output = resolved.OutputPath;
            }

            return merged.ApplyPopupAnalysis(new MergedApplyPopupAnalysisRequest(
                input,
                ReadStringOption(args, "--snapshot"),
                output,
                ReadIntOption(args, "--max-items"),
                HasFlag(args, "--skip-images")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "apply-scroll-snapshot")
        {
            var input = ReadStringOption(args, "--input") ?? ReadStringOption(args, "--analysis");
            if (string.IsNullOrWhiteSpace(input))
            {
                return OperationResult.Failure(
                    "merged.apply_scroll_snapshot",
                    "missing_required_option",
                    new { option = "--input" });
            }

            var (output, error) = ResolveMarkdownOutputPath(args, options, "merged.apply_scroll_snapshot");
            if (error is not null)
            {
                return error;
            }

            return merged.ApplyScrollSnapshot(new MergedApplyScrollSnapshotRequest(
                input,
                output!,
                ReadStringOption(args, "--snapshot"),
                ReadStringOption(args, "--hwnd") ?? ReadStringOption(args, "--window-handle"),
                ReadIntOption(args, "--max-items"),
                HasFlag(args, "--skip-images"),
                ReadIntOption(args, "--notches"),
                ReadIntOption(args, "--pixels") ?? ReadIntOption(args, "--amount"),
                ReadStringOption(args, "--index"),
                HasFlag(args, "--no-scroll")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "scroll-popup")
        {
            return merged.ScrollPopup(new MergedScrollPopupRequest(
                ReadStringOption(args, "--hwnd") ?? ReadStringOption(args, "--window-handle"),
                ReadIntOption(args, "--notches"),
                ReadIntOption(args, "--pixels") ?? ReadIntOption(args, "--amount")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "right-click-image")
        {
            var snapshot = ReadStringOption(args, "--snapshot");
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                return OperationResult.Failure("merged.right_click_image", "missing_required_option", new { option = "--snapshot" });
            }

            var x = ReadIntOption(args, "--x");
            var y = ReadIntOption(args, "--y");
            var w = ReadIntOption(args, "--w") ?? ReadIntOption(args, "--width");
            var h = ReadIntOption(args, "--h") ?? ReadIntOption(args, "--height");
            if (x is null || y is null || w is null || h is null)
            {
                return OperationResult.Failure("merged.right_click_image", "missing_required_option", new { option = "--x --y --w --h" });
            }

            return merged.RightClickImage(new MergedRightClickImageRequest(snapshot, x.Value, y.Value, w.Value, h.Value));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "snapshot-screen")
        {
            var (output, error) = ResolveMarkdownOutputPath(args, options, "merged.snapshot_screen");
            if (error is not null)
            {
                return error;
            }

            return merged.SnapshotScreen(new MergedSnapshotScreenRequest(
                output!,
                ReadStringOption(args, "--index")));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "click")
        {
            var snapshot = ReadStringOption(args, "--snapshot");
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                return OperationResult.Failure("merged.click", "missing_required_option", new { option = "--snapshot" });
            }

            var x = ReadIntOption(args, "--x");
            var y = ReadIntOption(args, "--y");
            var w = ReadIntOption(args, "--w") ?? ReadIntOption(args, "--width");
            var h = ReadIntOption(args, "--h") ?? ReadIntOption(args, "--height");
            if (x is null || y is null || w is null || h is null)
            {
                return OperationResult.Failure("merged.click", "missing_required_option", new { option = "--x --y --w --h" });
            }

            return merged.Click(new MergedClickRequest(snapshot, x.Value, y.Value, w.Value, h.Value));
        }

        if (args.Length >= 2 && args[0] == "merged" && args[1] == "append-clipboard-image")
        {
            string? output = null;
            if (HasOption(args, "--output") || HasOption(args, "--name") || HasOption(args, "--output-dir"))
            {
                var resolved = ResolveMarkdownOutputPath(args, options, "merged.append_clipboard_image");
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                output = resolved.OutputPath;
            }

            return merged.AppendClipboardImage(new MergedAppendClipboardImageRequest(
                ReadStringOption(args, "--snapshot"),
                output,
                ReadStringOption(args, "--speaker")));
        }

        if (args.Length >= 2 && args[0] == "capture" && args[1] == "start")
        {
            var (output, error) = ResolveMarkdownOutputPath(args, options, "capture.start");
            if (error is not null)
            {
                return error;
            }

            if (HasFlag(args, "--background"))
            {
                return StartBackgroundCapture(output!);
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            return capture.Start(new CaptureStartRequest(output!), cts.Token);
        }

        if (args.Length >= 2 && args[0] == "capture" && args[1] == "run-background")
        {
            var output = ReadStringOption(args, "--output");
            if (string.IsNullOrWhiteSpace(output))
            {
                return OperationResult.Failure("capture.run_background", "missing_required_option", new { option = "--output" });
            }

            using var cts = new CancellationTokenSource();
            var monitor = Task.Run(() => WaitForBackgroundStopRequest(cts));
            try
            {
                return capture.Start(new CaptureStartRequest(output), cts.Token);
            }
            finally
            {
                cts.Cancel();
                try
                {
                    monitor.Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // The capture process is already stopping; monitor cleanup is best-effort.
                }

                CleanupBackgroundCaptureState();
            }
        }

        if (args.Length >= 2 && args[0] == "capture" && args[1] == "stop")
        {
            return StopBackgroundCapture(HasFlag(args, "--force"));
        }

        if (args.Length >= 2 && args[0] == "capture" && args[1] == "status")
        {
            return GetBackgroundCaptureStatus();
        }

        if (args.Length >= 2 && args[0] == "capture" && args[1] == "logs")
        {
            return GetBackgroundCaptureLogs(ReadIntOption(args, "--tail") ?? 80);
        }

        return OperationResult.Failure("unknown", "unknown_command", new { args });
    }

    private static WxBridgeOptions LoadOptions()
    {
        var paths = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "wxbridge.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WxBridge",
                "wxbridge.json")
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WxBridgeOptions>(json, JsonOptions) ?? new WxBridgeOptions();
        }

        return new WxBridgeOptions();
    }

    private static string SaveOptions(WxBridgeOptions options)
    {
        var path = GetOptionsPathForWrite();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(options, JsonOptions));
        return path;
    }

    private static string GetOptionsPathForWrite()
    {
        var localPath = Path.Combine(Environment.CurrentDirectory, "wxbridge.json");
        if (File.Exists(localPath))
        {
            return localPath;
        }

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WxBridge",
            "wxbridge.json");
        return File.Exists(appDataPath) ? appDataPath : localPath;
    }

    private static OperationResult StartBackgroundCapture(string outputPath)
    {
        var existing = LoadBackgroundCaptureState();
        if (existing is not null && IsProcessRunning(existing.Pid))
        {
            return OperationResult.Failure(
                "capture.start",
                "capture_already_running",
                new { existing.Pid, existing.OutputPath, existing.LogPath });
        }

        CleanupBackgroundCaptureState();
        var runtimeDir = GetRuntimeDirectory();
        var logPath = Path.Combine(runtimeDir, "capture.log");
        File.WriteAllText(logPath, string.Empty);

        var startInfo = CreateBackgroundProcessStartInfo(logPath, Path.GetFullPath(outputPath));
        if (startInfo is null)
        {
            return OperationResult.Failure("capture.start", "process_path_unavailable");
        }

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                return OperationResult.Failure("capture.start", "background_start_failed");
            }

            var state = new BackgroundCaptureState(
                process.Id,
                Path.GetFullPath(outputPath),
                logPath,
                DateTimeOffset.Now);
            SaveBackgroundCaptureState(state);

            return OperationResult.Success(
                "capture.start",
                new
                {
                    background = true,
                    pid = process.Id,
                    output = state.OutputPath,
                    log = logPath
                });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("capture.start", "background_start_failed", new { ex.Message });
        }
    }

    private static ProcessStartInfo? CreateBackgroundProcessStartInfo(string logPath, string outputPath)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var arguments = new List<string>();
        var startInfo = new ProcessStartInfo(processPath)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.CurrentDirectory
        };

        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryPath = Path.Combine(AppContext.BaseDirectory, "WxBridge.Cli.dll");
            if (!File.Exists(entryPath))
            {
                return null;
            }

            arguments.Add(entryPath);
        }

        arguments.Add("capture");
        arguments.Add("run-background");
        arguments.Add("--output");
        arguments.Add(outputPath);
        arguments.Add("--log");
        arguments.Add(logPath);

        startInfo.Arguments = string.Join(" ", arguments.Select(QuoteArgument));
        return startInfo;
    }

    private static string QuoteArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void EnableBackgroundLogging(string[] args)
    {
        if (args.Length < 2 || args[0] != "capture" || args[1] != "run-background")
        {
            return;
        }

        var logPath = ReadStringOption(args, "--log");
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var logDirectory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var writer = TextWriter.Synchronized(new StreamWriter(stream) { AutoFlush = true });
        Console.SetOut(writer);
        Console.SetError(writer);
    }

    private static OperationResult StopBackgroundCapture(bool force)
    {
        var state = LoadBackgroundCaptureState();
        if (state is null || !IsProcessRunning(state.Pid))
        {
            CleanupBackgroundCaptureState();
            return OperationResult.Success("capture.stop", new { running = false });
        }

        File.WriteAllText(GetStopRequestPath(), DateTimeOffset.Now.ToString("O"));

        try
        {
            var process = Process.GetProcessById(state.Pid);
            if (!process.WaitForExit(5000) && force)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }

            var running = !process.HasExited;
            if (!running)
            {
                CleanupBackgroundCaptureState();
            }

            return OperationResult.Success(
                "capture.stop",
                new
                {
                    running,
                    pid = state.Pid,
                    force,
                    output = state.OutputPath,
                    log = state.LogPath
                });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("capture.stop", "stop_failed", new { state.Pid, ex.Message });
        }
    }

    private static OperationResult GetBackgroundCaptureStatus()
    {
        var state = LoadBackgroundCaptureState();
        if (state is null)
        {
            return OperationResult.Success("capture.status", new { running = false });
        }

        var running = IsProcessRunning(state.Pid);
        if (!running)
        {
            CleanupBackgroundCaptureState();
        }

        return OperationResult.Success(
            "capture.status",
            new
            {
                running,
                state.Pid,
                output = state.OutputPath,
                log = state.LogPath,
                startedAt = state.StartedAt
            });
    }

    private static OperationResult GetBackgroundCaptureLogs(int tail)
    {
        var state = LoadBackgroundCaptureState();
        var logPath = state?.LogPath ?? Path.Combine(GetRuntimeDirectory(), "capture.log");
        if (!File.Exists(logPath))
        {
            return OperationResult.Failure("capture.logs", "log_not_found", new { log = logPath });
        }

        string[] lines;
        using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            lines = reader.ReadToEnd().Split(
                Environment.NewLine,
                StringSplitOptions.None);
        }

        var count = Math.Clamp(tail, 1, 500);
        return OperationResult.Success(
            "capture.logs",
            new
            {
                log = logPath,
                tail = count,
                lines = lines.TakeLast(count)
            });
    }

    private static void WaitForBackgroundStopRequest(CancellationTokenSource cts)
    {
        var stopPath = GetStopRequestPath();
        while (!cts.IsCancellationRequested)
        {
            if (File.Exists(stopPath))
            {
                cts.Cancel();
                return;
            }

            Thread.Sleep(250);
        }
    }

    private static string GetRuntimeDirectory()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WxBridge");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetStatePath()
    {
        return Path.Combine(GetRuntimeDirectory(), "capture.state.json");
    }

    private static string GetStopRequestPath()
    {
        return Path.Combine(GetRuntimeDirectory(), "capture.stop");
    }

    private static BackgroundCaptureState? LoadBackgroundCaptureState()
    {
        var path = GetStatePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BackgroundCaptureState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveBackgroundCaptureState(BackgroundCaptureState state)
    {
        File.WriteAllText(GetStatePath(), JsonSerializer.Serialize(state, JsonOptions));
    }

    private static void CleanupBackgroundCaptureState()
    {
        TryDelete(GetStopRequestPath());

        var state = LoadBackgroundCaptureState();
        if (state is not null && IsProcessRunning(state.Pid))
        {
            return;
        }

        TryDelete(GetStatePath());
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static (string? OutputPath, OperationResult? Error) ResolveMarkdownOutputPath(
        string[] args,
        WxBridgeOptions options,
        string action)
    {
        var output = ReadStringOption(args, "--output");
        if (!string.IsNullOrWhiteSpace(output))
        {
            return (output, null);
        }

        var name = ReadStringOption(args, "--name") ?? options.Markdown.OutputName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, OperationResult.Failure(action, "missing_required_option", new { option = "--output or --name" }));
        }

        var markdownName = NormalizeMarkdownFileName(name);
        if (markdownName is null)
        {
            return (null, OperationResult.Failure(action, "invalid_markdown_name", new { name }));
        }

        var outputDirectory = ReadStringOption(args, "--output-dir") ?? options.Markdown.OutputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return (null, OperationResult.Failure(
                action,
                "missing_output_directory",
                new { option = "--output-dir", config = "markdown.outputDirectory" }));
        }

        return (Path.Combine(outputDirectory, markdownName), null);
    }

    private static string? NormalizeMarkdownFileName(string name)
    {
        var trimmed = name.Trim();
        if (Path.GetFileName(trimmed) != trimmed)
        {
            return null;
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        return trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + ".md";
    }

    private static int? ReadIntOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadStringOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name)
    {
        return args.Any(arg => arg == name);
    }

    private static bool HasOption(string[] args, string name)
    {
        return args.Take(args.Length - 1).Any(arg => arg == name);
    }

    private static OperationResult SwitchByIndex(string[] args, IChatSessionService sessions, string action)
    {
        var index = ReadIntOption(args, "--index");
        if (index is null)
        {
            return OperationResult.Failure(action, "missing_required_option", new { option = "--index" });
        }

        var switchResult = sessions.SwitchSession(new SwitchSessionRequest(index.Value));
        return switchResult.Ok
            ? switchResult
            : OperationResult.Failure(action, switchResult.Error ?? "switch_failed", switchResult.Data);
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private sealed record BackgroundCaptureState(int Pid, string OutputPath, string LogPath, DateTimeOffset StartedAt);
}

