# WxBridge

WxBridge is a local Windows CLI bridge for controlling WeChat Desktop through deterministic automation.

## Install

WxBridge is distributed from GitHub Releases.

Recommended install for most users:

```powershell
iwr https://raw.githubusercontent.com/<owner>/WxBridge/main/packaging/install.ps1 -OutFile install.ps1
.\install.ps1 -Owner <owner> -Repo WxBridge -AddToPath -InstallSkill
```

The default package is small and requires the .NET 9 Desktop Runtime. If the runtime is not installed, the installer will show the download link. To install the bundled single-file build instead:

```powershell
.\install.ps1 -Owner <owner> -Repo WxBridge -SingleFile -AddToPath -InstallSkill
```

By default the CLI is installed to `%LOCALAPPDATA%\WxBridge`. The installer also:

- creates `%LOCALAPPDATA%\WxBridge\wxbridge.cmd`;
- sets the user environment variable `WXBRIDGE_HOME` to the install directory;
- optionally adds the install directory to the user `PATH` when `-AddToPath` is used;
- when `-InstallSkill` is used, writes the installed CLI path into the skill file `references/local-install.json`.

After installation, verify the CLI with:

```powershell
& "$env:WXBRIDGE_HOME\wxbridge.cmd" status
```

To install or refresh only the Codex skill from a cloned copy of this repository:

```powershell
.\packaging\install-skill.ps1
```

After installation, start a new Codex thread and ask it to use `wxbridge`, for example:

```text
使用 wxbridge，把当前微信可视区域聊天记录导出到 001
```

## Release

Maintainers can build release artifacts with:

```powershell
.\packaging\publish.ps1
```

This creates:

```text
dist/WxBridge-win-x64.zip
dist/WxBridge-win-x64-single-file.zip
dist/install.ps1
dist/checksums.txt
```

Upload these files to a GitHub Release. The small `WxBridge-win-x64.zip` package is framework-dependent; the single-file package is larger but does not require installing .NET separately.

Current scope:

- List chat sessions: API reserved, implementation pending UI Automation/OCR probing.
- Inspect the WeChat UI Automation tree to determine whether chat names are readable.
- Switch chat session by index: implemented with Win32 window activation and OS-level mouse input.
- Send text, files, or the current clipboard to the focused WeChat chat.
- Export a legacy visible-chat prototype by scanning left/right avatar bands. This is not the recommended Codex/Skill export path.
- Snapshot the current visible chat area so Codex can read the screenshot, then apply Codex's structured analysis to write Markdown and copy image messages from WeChat.
- Export merged-forwarded chat-record popups through Codex-assisted screenshots, popup clicking, popup scrolling, and duplicate-aware Markdown writing.

## Commands

For interactive use, prefer the wrapper script after the first build. It reuses the compiled executable instead of paying `dotnet run` startup cost on every command:

```powershell
.\wxbridge.ps1 -Rebuild status
.\wxbridge.ps1 merged snapshot-entry --name "merged-capture"
```

```powershell
dotnet run --project .\src\WxBridge.Cli -- status
dotnet run --project .\src\WxBridge.Cli -- sessions list
dotnet run --project .\src\WxBridge.Cli -- sessions inspect --view raw --max-depth 6 --limit 300
dotnet run --project .\src\WxBridge.Cli -- sessions switch --index 1
dotnet run --project .\src\WxBridge.Cli -- sessions open --name "contacta"
dotnet run --project .\src\WxBridge.Cli -- messages send-text --text "hello"
dotnet run --project .\src\WxBridge.Cli -- messages send-file --path "<path-to-file>"
dotnet run --project .\src\WxBridge.Cli -- messages send-clipboard
dotnet run --project .\src\WxBridge.Cli -- messages send-text-to --index 1 --text "hello"
dotnet run --project .\src\WxBridge.Cli -- messages send-file-to --index 1 --path "<path-to-video>"
dotnet run --project .\src\WxBridge.Cli -- messages send-clipboard-to --index 1
dotnet run --project .\src\WxBridge.Cli -- messages snapshot-visible --name "visible-capture"
dotnet run --project .\src\WxBridge.Cli -- messages apply-visible-analysis --input "<captures-dir>\visible-capture_assets\visible-chat-analysis.json"
dotnet run --project .\src\WxBridge.Cli -- merged snapshot-entry --name "merged-capture"
dotnet run --project .\src\WxBridge.Cli -- merged open-entry --snapshot "<captures-dir>\merged-capture_assets\merged-entry-snapshot.json" --x 100 --y 100 --w 240 --h 120
dotnet run --project .\src\WxBridge.Cli -- merged open-entry-and-snapshot --snapshot "<captures-dir>\merged-capture_assets\merged-entry-snapshot.json" --x 100 --y 100 --w 240 --h 120 --name "merged-capture" --index 001
dotnet run --project .\src\WxBridge.Cli -- merged snapshot-popup --name "merged-capture" --hwnd "0x123456" --index 001
dotnet run --project .\src\WxBridge.Cli -- merged apply-popup-analysis --input "<captures-dir>\merged-capture_assets\merged-popup-analysis-001.json"
dotnet run --project .\src\WxBridge.Cli -- merged apply-scroll-snapshot --input "<captures-dir>\merged-capture_assets\merged-popup-analysis-001.json" --name "merged-capture" --index 002
dotnet run --project .\src\WxBridge.Cli -- merged apply-popup-analysis --input "<captures-dir>\merged-capture_assets\merged-popup-analysis-001.json" --skip-images
dotnet run --project .\src\WxBridge.Cli -- merged scroll-popup --hwnd "0x123456"
dotnet run --project .\src\WxBridge.Cli -- capture start --output "<captures-dir>\chat.md"
dotnet run --project .\src\WxBridge.Cli -- capture start --name "manual-capture"
dotnet run --project .\src\WxBridge.Cli -- capture start --background
dotnet run --project .\src\WxBridge.Cli -- capture status
dotnet run --project .\src\WxBridge.Cli -- capture stop
dotnet run --project .\src\WxBridge.Cli -- config set-output-dir --path "<captures-dir>"
dotnet run --project .\src\WxBridge.Cli -- config set-output-name --name "manual-capture"
dotnet run --project .\src\WxBridge.Cli -- config set-self-speaker --name "我"
```

## Manual Capture Quick Start

Set the default Markdown output directory and your own speaker name once:

```powershell
dotnet run --project .\src\WxBridge.Cli -- config set-output-dir --path "<captures-dir>"
dotnet run --project .\src\WxBridge.Cli -- config set-self-speaker --name "我"
```

Then start a background capture session:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start --background
```

During capture:

- Right-click another person's avatar, then click the `@name` menu item to set that person as the current Markdown speaker.
- Right-click your own avatar, then left-click away to close the menu and set the configured self speaker.
- Right-click a message and click copy to append its text or image content under the current speaker.
- Press `Ctrl+C` in the terminal to stop capture.

For background capture, stop it with:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture stop
```

See `docs/cli.md` for the full workflow and troubleshooting notes.

## Codex-Assisted Visible Export

This flow keeps model work inside Codex. WxBridge only captures screenshots and performs deterministic Windows actions.

Step 1: capture the current visible WeChat chat area:

```powershell
dotnet run --project .\src\WxBridge.Cli -- messages snapshot-visible --name "visible-capture"
```

The command returns the screenshot path, the screen-region coordinates, a snapshot manifest path, and a suggested analysis JSON path.

Step 2: let Codex read the screenshot and write an analysis JSON:

```json
{
  "snapshot": "<captures-dir>\\visible-capture_assets\\visible-chat-snapshot.json",
  "copyPoints": [
    { "speaker": "sender name or empty", "role": "self|other", "type": "text|image", "x": 0, "y": 0 }
  ]
}
```

Step 3: apply the analysis:

```powershell
dotnet run --project .\src\WxBridge.Cli -- messages apply-visible-analysis --input "<captures-dir>\visible-capture_assets\visible-chat-analysis.json"
```

Text and image messages are copied from WeChat by right-clicking the screenshot-relative point provided by Codex. Text in JSON is only a fallback when copy fails. The user's original clipboard is restored after the command finishes.

## Merged Chat Record Export

Use `merged` commands for a WeChat merged-forwarded chat-record card.

1. Capture the main chat area:

```powershell
dotnet run --project .\src\WxBridge.Cli -- merged snapshot-entry --name "example-group"
```

2. Let Codex inspect the returned screenshot and identify the merged-record card bbox. Open it and capture the popup in one command:

```powershell
.\wxbridge.ps1 merged open-entry-and-snapshot --snapshot "<captures-dir>\example-group_assets\merged-entry-snapshot.json" --x 1030 --y 260 --w 260 --h 120 --name "example-group" --index 001
```

3. Let Codex inspect the popup screenshot and write analysis JSON, then apply that screen, scroll, and capture the next screen in one command:

```powershell
.\wxbridge.ps1 merged apply-scroll-snapshot --input "<captures-dir>\example-group_assets\merged-popup-analysis-001.json" --name "example-group" --index 002
```

4. Repeat step 3 with the next analysis JSON until `screenshotHash` no longer changes. For the last screen, use `--no-scroll` if no next snapshot is needed:

```powershell
.\wxbridge.ps1 merged apply-scroll-snapshot --input "<captures-dir>\example-group_assets\merged-popup-analysis-002.json" --name "example-group" --no-scroll
```

For long popups, apply each screen's analysis directly. Codex can provide a lightweight `copyPoints` list with only message type, speaker, and screenshot-relative right-click point; WxBridge right-click copies both text and image messages from the visible popup during `apply-popup-analysis`, then scrolls from a safe lower-right popup area without clicking message content. The older `messages + bbox` format is still supported, and analysis JSON text is only a fallback when text copy fails.

`merged apply-popup-analysis` keeps `merged-export-state.json` in the assets folder so repeated screens do not duplicate clipboard-copied text or copied images.

If a popup `--hwnd` becomes invalid, WxBridge automatically falls back to finding a visible window whose title contains `wechat.mergedPopupTitleKeyword`.

`--skip-images` is still available as a troubleshooting option when you want to write text first and leave image recovery for later.

## Configuration

WxBridge reads `wxbridge.json` from the current directory first, then `%APPDATA%\WxBridge\wxbridge.json`.

The first switching implementation uses coordinates relative to the WeChat main window:

```json
{
  "markdown": {
    "outputDirectory": "<captures-dir>",
    "outputName": "manual-capture.md",
    "selfSpeakerName": "我"
  },
  "wechat": {
    "windowTitleKeyword": "微信",
    "chatListClickXOffset": 180,
    "chatListTopYOffset": 92,
    "chatItemHeight": 64,
    "searchBoxClickXOffset": 180,
    "searchBoxClickYOffset": 48,
    "searchFocusPauseMs": 80,
    "searchKeywordPauseMs": 120,
    "searchResultDelayMs": 300,
    "searchOpenDelayMs": 180,
    "pasteDelayMs": 120,
    "sendDelayMs": 120,
    "visibleExportLeftInset": 20,
    "visibleExportRightInset": 10,
    "visibleExportScanStep": 5,
    "visibleExportImageCopyWaitMs": 220,
    "mergedPopupTitleKeyword": "聊天记录",
    "mergedPopupOpenWaitMs": 500,
    "mergedPopupScrollDelayMs": 250
  }
}
```


