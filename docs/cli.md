# CLI

All commands return JSON.

## status

```powershell
wxbridge status
```

## sessions list

```powershell
wxbridge sessions list
```

Reserved API. The implementation will use UI Automation first, OCR second.

## sessions inspect

```powershell
wxbridge sessions inspect --view control --max-depth 6 --limit 300
wxbridge sessions inspect --view raw --max-depth 8 --limit 500 --include-empty
```

Inspects the WeChat main window through Windows UI Automation and returns readable control names, types, automation IDs, classes, and bounds. Use this before implementing `sessions list`.

## sessions switch

```powershell
wxbridge sessions switch --index 1
wxbridge sessions switch --name "contacta"
```

Switches to the Nth chat item in the visible chat list by activating WeChat and clicking the calculated row position.

With `--name`, WxBridge opens the WeChat search box, pastes the given keyword, waits for search results, and presses Enter to open the first result.

When invoking this command from natural language, convert Chinese names or group keywords to Hanyu Pinyin before constructing the CLI command. Do not translate Chinese names by meaning. For example, `文件传输助手` should call `wxbridge sessions switch --name "wenjianchuanshuzhushou"`. Pinyin search avoids WeChat's Chinese web-search suggestions and is more stable for opening the intended chat.

## sessions open

```powershell
wxbridge sessions open --name "contacta"
wxbridge sessions open --query "xiangmuqun"
```

Searches WeChat by contact or group keyword and opens the first result. This is the preferred command when operating from natural language because it does not require knowing the chat's current position in the visible list.

The final `--name` or `--query` value should be a pinyin search keyword, not the original Chinese text. For example:

```text
文件传输助手 -> wenjianchuanshuzhushou
张三 -> zhangsan
项目群 -> xiangmuqun
```

This convention is especially important for LLM callers that turn a user instruction into a CLI command: convert the Chinese keyword to pinyin first, then call `sessions open` or `sessions switch`. Do not use English translations such as `filetransferassistant`.

The search implementation uses these configurable offsets relative to the WeChat main window:

```json
{
  "wechat": {
    "searchBoxClickXOffset": 180,
    "searchBoxClickYOffset": 48,
    "searchBoxClickCount": 2,
    "searchBoxClickGapMs": 90,
    "searchFocusPauseMs": 80,
    "searchKeywordPauseMs": 120,
    "searchResultDelayMs": 300,
    "searchOpenDelayMs": 180
  }
}
```

`searchFocusPauseMs` pauses after clicking the search box. `searchKeywordPauseMs` pauses after pasting the keyword. Keep them higher while debugging so the two visible UI states are easy to confirm, then reduce them later for speed.

If Enter does not select the expected first result on your WeChat version, adjust the delays first. A click-based first-result strategy can be added later if needed.

## messages send-text

```powershell
wxbridge messages send-text --text "hello"
```

Writes text to the Windows clipboard, pastes it into the current WeChat chat, and presses Enter.

## messages send-file

```powershell
wxbridge messages send-file --path "<path-to-file>"
wxbridge messages send-file --path "<path-to-video>"
```

Writes a file drop list to the Windows clipboard, pastes it into the current WeChat chat, and presses Enter.

## messages send-clipboard

```powershell
wxbridge messages send-clipboard
```

Pastes and sends whatever is already in the Windows clipboard.

## Combined Commands

```powershell
wxbridge messages send-text-to --index 1 --text "hello"
wxbridge messages send-file-to --index 1 --path "<path-to-file>"
wxbridge messages send-clipboard-to --index 1
```

These commands switch to the indexed chat first, then send the payload.

## messages export-visible

```powershell
wxbridge messages export-visible --output "<captures-dir>\chat.md"
wxbridge messages export-visible --name "manual-capture"
wxbridge messages export-visible --output-dir "<captures-dir>" --name "manual-capture"
wxbridge messages export-visible --output "<captures-dir>\chat.md" --left-inset 20 --right-inset 10 --scan-step 5
wxbridge messages export-visible --output "<captures-dir>\chat.md" --resolve-names --copy-content --max-items 1
```

Captures the visible chat message area, scans narrow avatar bands on the left and right, saves avatar crops, and generates a Markdown prototype.

`--output` accepts the full Markdown file path. `--name` accepts only the final Markdown file name; `.md` is added automatically when omitted. `--output-dir` overrides the configured `markdown.outputDirectory` for one run.

`--resolve-names` right-clicks avatar candidates, clicks the `@name` menu item, reads the inserted input-box text, and clears the input box.

`--copy-content` right-clicks the estimated message content point, clicks the context-menu copy item, and writes clipboard text into Markdown.

## messages snapshot-visible

```powershell
wxbridge messages snapshot-visible --name "visible-capture"
wxbridge messages snapshot-visible --output "<captures-dir>\visible-capture.md"
```

Captures the current visible WeChat chat area and saves a screenshot next to the target Markdown file. This command does not call any model. It returns enough information for Codex to inspect the screenshot and produce structured analysis.

Returned data includes:

- `output`: target Markdown path;
- `screenshot`: local PNG path for Codex to inspect;
- `snapshot`: local JSON manifest with output, screenshot, and screen-region coordinates;
- `suggestedAnalysis`: suggested path where Codex can write analysis JSON;
- `region`: screen coordinates of the captured chat area.

## messages apply-visible-analysis

```powershell
wxbridge messages apply-visible-analysis --input "<captures-dir>\visible-capture_assets\visible-chat-analysis.json"
wxbridge messages apply-visible-analysis --input "<analysis-json>" --snapshot "<captures-dir>\visible-capture_assets\visible-chat-snapshot.json"
```

Applies a Codex-generated analysis JSON to the active WeChat window. Codex should only identify message ownership, message type, and a safe right-click copy point. WxBridge right-click copies text and image messages from WeChat, writes copied text into Markdown, saves copied images to the Markdown assets folder, and restores the original clipboard afterwards.

The preferred lightweight analysis JSON references the snapshot manifest:

```json
{
  "snapshot": "<captures-dir>\\visible-capture_assets\\visible-chat-snapshot.json",
  "copyPoints": [
    { "speaker": "contact-a", "role": "other", "type": "text", "x": 180, "y": 245 },
    { "speaker": "我", "role": "self", "type": "image", "x": 620, "y": 390 }
  ]
}
```

`x` and `y` are relative to the screenshot image returned by `snapshot-visible`, not absolute screen coordinates. WxBridge converts them back to screen coordinates using `snapshot.region`. For messages sent by the user, prefer `role: "self"`; WxBridge will use `markdown.selfSpeakerName` when configured. For other people or group members, pass the visible sender name in `speaker`.

The older `messages + bbox` analysis JSON is still supported. In that mode, `bbox` coordinates are also relative to the screenshot. Text in JSON is only used as a fallback if right-click copy fails.

If `analysis.json` does not include `snapshot`, pass `--snapshot`. If neither the analysis nor snapshot includes `output`, pass `--output` or `--name`.

Minimal analysis JSON also works if `--snapshot` is provided:

```json
[
  {
    "speaker": "contact-a",
    "role": "other",
    "type": "text",
    "text": "这是一条消息",
    "bbox": { "x": 80, "y": 120, "width": 260, "height": 42 }
  }
]
```

If an image copy fails, the Markdown file receives a placeholder line instead of silently dropping the message:

```md
> [图片复制失败，需要人工补充：clipboard_has_no_image_after_copy]
```

The first version intentionally focuses on the current visible area only. Full-history scrolling, de-duplication across screens, voice messages, files, videos, and stickers should be added as separate iterations.

## merged snapshot-entry

```powershell
wxbridge merged snapshot-entry --name "merged-capture"
wxbridge merged snapshot-entry --output "<captures-dir>\merged-capture.md"
```

Captures the main WeChat chat area so Codex can locate the merged-forwarded chat-record card. The returned snapshot uses the same coordinate contract as visible export: `region` is absolute screen coordinates, while later bbox values are relative to the screenshot.

Returned data includes `screenshot`, `snapshot`, `suggestedAnalysis`, `screenshotHash`, and `region`.

## merged open-entry

```powershell
wxbridge merged open-entry --snapshot "<captures-dir>\...\merged-entry-snapshot.json" --x 1030 --y 260 --w 260 --h 120
```

Clicks the center of the Codex-provided bbox to open the merged chat-record popup. The bbox is relative to the `snapshot-entry` screenshot. WxBridge converts it to screen coordinates, clicks, waits for a popup whose title contains `wechat.mergedPopupTitleKeyword`, and returns the popup `windowHandle`.

## merged open-entry-and-snapshot

```powershell
wxbridge merged open-entry-and-snapshot --snapshot "<captures-dir>\...\merged-entry-snapshot.json" --x 1030 --y 260 --w 260 --h 120 --name "merged-capture" --index 001
```

Optimized command for Codex/Skill workflows. It opens the merged chat-record card and immediately captures the popup in the same CLI process. This replaces:

```text
open-entry -> snapshot-popup
```

Returned data contains both `open` and `snapshot`; use the popup screenshot path from `snapshot.screenshot` for the next Codex analysis step.

## merged snapshot-popup

```powershell
wxbridge merged snapshot-popup --name "merged-capture" --hwnd "0x123456" --index 001
wxbridge merged snapshot-popup --name "merged-capture" --index 002
```

Captures the merged chat-record popup. Prefer passing `--hwnd` from `merged open-entry`; if omitted, WxBridge finds the first visible window whose title contains `聊天记录`.

The snapshot manifest includes:

```json
{
  "output": "<captures-dir>\\merged-capture.md",
  "screenshot": "<captures-dir>\\merged-capture_assets\\merged-popup-snapshot-001.png",
  "region": { "x": 522, "y": 28, "width": 560, "height": 720 },
  "windowKind": "popup",
  "windowHandle": "0x123456",
  "screenshotHash": "...",
  "suggestedAnalysis": "<captures-dir>\\merged-capture_assets\\merged-popup-analysis-001.json",
  "index": "001"
}
```

## merged apply-popup-analysis

```powershell
wxbridge merged apply-popup-analysis --input "<captures-dir>\...\merged-popup-analysis-001.json"
wxbridge merged apply-popup-analysis --input "<analysis-json>" --snapshot "<captures-dir>\...\merged-popup-snapshot-001.json"
wxbridge merged apply-popup-analysis --input "<captures-dir>\...\merged-popup-analysis-001.json" --skip-images
```

Applies Codex-generated popup analysis to Markdown. Codex only needs to identify message type, speaker, and bbox. Text messages are right-click copied from the popup by bbox and written from the clipboard; `text` in the analysis JSON is used only as a fallback if copying fails. Image messages are also right-click copied from the popup by bbox, saved under the Markdown assets folder, and referenced in Markdown.

Preferred lightweight analysis JSON:

```json
{
  "snapshot": "<captures-dir>\\merged-capture_assets\\merged-popup-snapshot-001.json",
  "copyPoints": [
    { "speaker": "sender-a", "type": "text", "x": 180, "y": 245 },
    { "speaker": "阳", "type": "image", "x": 210, "y": 390 }
  ]
}
```

`x` and `y` are relative to the popup screenshot. Codex should choose a point inside the message content that is safe to right-click. WxBridge turns each point into a copy action, reads the clipboard, writes Markdown, and de-duplicates the result.

The older `messages + bbox` analysis JSON is still supported:

```json
{
  "snapshot": "<captures-dir>\\merged-capture_assets\\merged-popup-snapshot-001.json",
  "messages": [
    {
      "speaker": "sender-a",
      "role": "other",
      "type": "text",
      "text": "@recipient-a 这里更新包含张总的一些更新和定位的问题",
      "bbox": { "x": 72, "y": 132, "w": 410, "h": 60 },
      "confidence": 0.94
    },
    {
      "speaker": "阳",
      "role": "other",
      "type": "image",
      "text": "",
      "bbox": { "x": 72, "y": 380, "w": 198, "h": 160 },
      "confidence": 0.9
    }
  ]
}
```

`merged apply-popup-analysis` maintains `merged-export-state.json` in the Markdown assets folder. It skips duplicate text by the normalized clipboard text copied from WeChat and skips duplicate images by SHA256 of the copied image.

For normal merged-record export, do not use `--skip-images`: WxBridge will directly right-click-copy image messages from the current visible popup while the source messages are still on screen. Use `--skip-images` only as a troubleshooting option when you intentionally want to write text first and recover images later.

The command returns `writtenTexts`, `copiedTexts`, `failedTexts`, `fallbackTexts`, `copiedImages`, `skippedImages`, `skippedDuplicates`, and `failedImages`.

## merged apply-scroll-snapshot

```powershell
wxbridge merged apply-scroll-snapshot --input "<captures-dir>\...\merged-popup-analysis-001.json" --name "merged-capture" --index 002
wxbridge merged apply-scroll-snapshot --input "<captures-dir>\...\merged-popup-analysis-002.json" --name "merged-capture" --no-scroll
```

Optimized command for long merged-record popups. It applies the current popup analysis, scrolls the popup, then captures the next popup screenshot in one CLI process. This replaces:

```text
apply-popup-analysis -> scroll-popup -> snapshot-popup
```

Returned data contains `apply`, `scroll`, and `snapshot`. Codex should inspect `snapshot.screenshot`, write the next analysis JSON, then call `apply-scroll-snapshot` again.

Use `--no-scroll` for the last screen when Codex knows there is no more content to capture. `--pixels`, `--amount`, `--notches`, `--skip-images`, and `--max-items` are passed through to the underlying operations.

## merged scroll-popup

```powershell
wxbridge merged scroll-popup --hwnd "0x123456"
wxbridge merged scroll-popup --hwnd "0x123456" --pixels 420
wxbridge merged scroll-popup --notches -3
```

Activates the popup, moves the mouse to a safe lower-right area of the popup, and scrolls without left-clicking the content.

By default, WxBridge chooses a wheel amount from the popup height: 8 notches for height >= 720 px, 6 notches for 560-719 px, and 4 notches below 560 px. Use `--notches` to override this directly. `--pixels` is still available for low-level debugging, but it is converted to wheel notches and is less reliable than explicit notches.

If the supplied `--hwnd` is no longer valid, WxBridge falls back to locating a visible popup whose title contains `wechat.mergedPopupTitleKeyword`.

For long merged records, Codex should repeat:

```text
inspect screenshot -> write analysis JSON -> apply-scroll-snapshot
```

Stop when `screenshotHash` no longer changes after scrolling, or when a screen produces no new `writtenTexts` or `copiedImages`.

## merged right-click-image

```powershell
wxbridge merged right-click-image --snapshot "<captures-dir>\...\merged-popup-snapshot-001.json" --x 80 --y 132 --w 247 --h 82
```

Low-level troubleshooting command. Right-clicks a Codex-identified image bbox inside a popup snapshot. This command intentionally does not click the context menu.

The bbox is relative to the snapshot image. WxBridge clicks near the image edge instead of the center to reduce the chance of opening image preview.

## merged snapshot-screen

```powershell
wxbridge merged snapshot-screen --name "merged-capture" --index menu-001
```

Captures the full virtual screen. Use this immediately after `right-click-image` so Codex can see the context menu, even if the menu appears outside the popup bounds.

## merged click

```powershell
wxbridge merged click --snapshot "<captures-dir>\...\merged-screen-snapshot-menu-001.json" --x 10 --y 10 --w 80 --h 30
```

Clicks the center of a bbox relative to any WxBridge snapshot. This is mainly useful for troubleshooting low-level UI actions.

## merged append-clipboard-image

```powershell
wxbridge merged append-clipboard-image --snapshot "<captures-dir>\...\merged-popup-snapshot-001.json" --speaker "sender-a"
wxbridge merged append-clipboard-image --output "<captures-dir>\merged-capture.md" --speaker "sender-a"
```

Reads the current Windows clipboard image and appends it to the Markdown document. The target output path can come from `--output` or from the provided snapshot. Duplicate images are skipped using the same `merged-export-state.json` image hash set.

Troubleshooting original-image loop:

```text
1. apply-popup-analysis --skip-images
2. right-click-image for one image bbox
3. snapshot-screen
4. Codex identifies the context-menu Copy item
5. click that Copy item bbox
6. append-clipboard-image --speaker ...
```

The normal merged-record export path no longer requires this loop.

## config set-output-dir

```powershell
wxbridge config set-output-dir --path "<captures-dir>"
```

Stores the default Markdown output directory in `wxbridge.json`. After this, capture/export commands can use `--name` instead of passing a full `--output` path.

## config set-output-name

```powershell
wxbridge config set-output-name --name "manual-capture"
```

Stores the default Markdown file name in `wxbridge.json`. `.md` is added automatically when omitted. After this, capture/export commands can omit both `--output` and `--name` as long as `markdown.outputDirectory` is configured.

## config set-self-speaker

```powershell
wxbridge config set-self-speaker --name "我"
```

Stores the Markdown speaker name used when capture mode detects a right-click on your own avatar.

## Manual WeChat Capture Workflow

Use `capture start` when you want to manually save visible WeChat messages into a Markdown file.

### First-time setup

Set the folder where Markdown files should be saved:

```powershell
dotnet run --project .\src\WxBridge.Cli -- config set-output-dir --path "<captures-dir>"
```

Set the default Markdown file name:

```powershell
dotnet run --project .\src\WxBridge.Cli -- config set-output-name --name "manual-capture"
```

Set the speaker name used for your own messages:

```powershell
dotnet run --project .\src\WxBridge.Cli -- config set-self-speaker --name "我"
```

These commands write to `wxbridge.json`. After setup, you can use `--name` instead of passing a full file path every time.

### Start capture

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start --name "manual-capture"
```

If both `markdown.outputDirectory` and `markdown.outputName` are configured, you can omit the name:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start
```

To run capture in the background and return immediately:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start --background
```

This writes to:

```text
<captures-dir>\manual-capture.md
```

If the file already exists, new captured content is appended to it. Image content is saved under an assets folder next to the Markdown file.

You can still override the full output path for a single run:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start --output "<captures-dir>\manual-capture.md"
```

You can also override only the directory for a single run:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start --output-dir "<temp-captures-dir>" --name "today"
```

The same output options work with background capture:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture start --background --name "today"
```

### Capture another person's message

1. Right-click that person's avatar on the left side of the chat.
2. Click the `@name` menu item.
3. WxBridge reads the inserted `@name` text from the WeChat input box, sets it as the current Markdown speaker, and clears the input box.
4. Right-click that person's message bubble or content.
5. Click copy.
6. WxBridge appends the copied text or image under the current speaker in Markdown.

### Capture your own message

1. Right-click your own avatar on the right side of the chat.
2. Left-click an empty area to close the menu. Do not click `拍一拍`.
3. WxBridge sets the current Markdown speaker to `markdown.selfSpeakerName`.
4. Right-click your own message bubble or content.
5. Click copy.
6. WxBridge appends the copied text or image under your configured speaker name.

### Recommended operating rhythm

Set the speaker first, then copy one or more messages from that speaker:

```text
right-click avatar -> set speaker
right-click message -> copy content
right-click message -> copy content
right-click another avatar -> set speaker
right-click message -> copy content
```

Message ownership is not inferred from where the mouse is when copying content. Long messages can span across the chat area, so WxBridge only changes the speaker when you operate on an avatar.

### Stop capture

Press `Ctrl+C` in the terminal that is running `capture start`.

If capture is running in the background, stop it with:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture stop
```

If the background process does not exit after a normal stop request:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture stop --force
```

Check whether background capture is running:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture status
```

Read recent background capture logs:

```powershell
dotnet run --project .\src\WxBridge.Cli -- capture logs --tail 80
```

### Common issues

- If a message is written under the wrong speaker, set the speaker again by operating on the correct avatar, then copy the message again.
- If another person's name is not detected after clicking `@name`, wait for the input box to update and try the avatar action again.
- If your own messages are shown as `我`, change the configured name with `config set-self-speaker`.
- If `capture start --name ...` reports `missing_output_directory`, run `config set-output-dir` first or pass `--output-dir`.
- If `capture start --background` reports `capture_already_running`, use `capture status` to inspect the existing process or `capture stop` before starting another one.

## capture start

```powershell
wxbridge capture start --output "<captures-dir>\chat.md"
wxbridge capture start --name "manual-capture"
wxbridge capture start --output-dir "<captures-dir>" --name "manual-capture"
wxbridge capture start --background
```

Starts a capture session. Without `--background`, the command keeps running until you press `Ctrl+C`. With `--background`, the command starts a hidden capture process and returns JSON immediately.

- Right-click an avatar and click `@name` to set the current Markdown speaker.
- Right-click your own avatar, then left-click away to close the menu and set the configured self speaker.
- Right-click a message and click copy to append clipboard text or image content.
- Press `Ctrl+C` in the terminal to stop.

## capture stop

```powershell
wxbridge capture stop
wxbridge capture stop --force
```

Stops the background capture process. `--force` kills the process if it does not exit after the normal stop request.

## capture status

```powershell
wxbridge capture status
```

Returns whether background capture is running, along with the PID, Markdown output path, log path, and start time when available.

## capture logs

```powershell
wxbridge capture logs --tail 80
```

Returns recent lines from the background capture log.


