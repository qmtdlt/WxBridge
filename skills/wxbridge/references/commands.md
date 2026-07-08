# WxBridge Command Reference

## Basics

Resolve the command before running workflows:

- Prefer `references/local-install.json` from the installed skill. Use its `command` field.
- If that file is missing, use `$env:WXBRIDGE_HOME\wxbridge.cmd`.
- If `WXBRIDGE_HOME` is missing, try `wxbridge` from `PATH`.
- If PATH is not refreshed yet, try `$env:LOCALAPPDATA\WxBridge\wxbridge.cmd`.

Example:

```powershell
$wxbridge = "$env:WXBRIDGE_HOME\wxbridge.cmd"
& $wxbridge status
```

## Install From GitHub

Install both the CLI and this Codex skill:

```powershell
iwr https://raw.githubusercontent.com/<owner>/WxBridge/main/packaging/install.ps1 -OutFile install.ps1
.\install.ps1 -Owner <owner> -Repo WxBridge -AddToPath -InstallSkill
```

The default install location is `%LOCALAPPDATA%\WxBridge`. The installer also sets the user environment variable `WXBRIDGE_HOME` and writes `references/local-install.json` into the installed skill so Codex can find the CLI even when PATH has not refreshed.

Use `-SingleFile` when the machine should not install the .NET 9 Desktop Runtime separately:

```powershell
.\install.ps1 -Owner <owner> -Repo WxBridge -SingleFile -AddToPath -InstallSkill
```

Use `.\wxbridge.ps1 -Rebuild status` only when working inside a cloned development repository after code changes.

## Uninstall For Clean Testing

Remove the installed CLI and installed Codex skill before repeat install tests:

```powershell
iwr https://raw.githubusercontent.com/<owner>/WxBridge/main/packaging/uninstall.ps1 -OutFile uninstall.ps1
.\uninstall.ps1 -RemoveFromPath
```

The uninstall script removes the default CLI install directory, the installed `wxbridge` skill, and `WXBRIDGE_HOME` when it points to that install directory. `-RemoveFromPath` also removes the install directory from the user PATH. Exported Markdown files are not removed.

## Open Chat

Use Hanyu Pinyin for search names. Do not translate Chinese names by meaning.

```powershell
& $wxbridge sessions open --name "wenjianchuanshuzhushou"
& $wxbridge sessions open --name "lizi"
```

The CLI searches WeChat and opens the first result. For Chinese user requests, convert the target name to Hanyu Pinyin before calling the command.

Important examples:

- `文件传输助手` -> `wenjianchuanshuzhushou`
- `张三` -> `zhangsan`
- Do not use English translations such as `filetransferassistant`.

## Markdown Configuration

Set default output directory:

```powershell
& $wxbridge config set-output-dir --path "<captures-dir>"
```

Set default Markdown file name:

```powershell
& $wxbridge config set-output-name --name "ABC"
```

Set the user's own speaker name:

```powershell
& $wxbridge config set-self-speaker --name "self-name"
```

## Visible Chat Export

Do not use `messages export-visible` for user-facing exports. It is an older prototype that scans avatar bands and can generate incomplete Markdown. The supported workflow is:

```text
snapshot-visible -> Codex writes copyPoints -> apply-visible-analysis
```

1. Snapshot current visible chat:

```powershell
& $wxbridge messages snapshot-visible --name "visible-test"
```

2. Inspect `data.screenshot`. Write `data.suggestedAnalysis` as lightweight JSON:

```json
{
  "snapshot": "<captures-dir>\\visible-test_assets\\visible-chat-snapshot.json",
  "copyPoints": [
    { "speaker": "sender-a", "role": "other", "type": "text", "x": 180, "y": 245 },
    { "speaker": "self-name", "role": "self", "type": "image", "x": 620, "y": 390 }
  ]
}
```

3. Apply:

```powershell
& $wxbridge messages apply-visible-analysis --input "<captures-dir>\visible-test_assets\visible-chat-analysis.json"
```

Check `writtenTexts`, `copiedTexts`, `failedTexts`, `copiedImages`, and `failedImages`. Retry failed items with better points.

## Merged Chat Record Export

1. Snapshot the main chat area:

```powershell
& $wxbridge merged snapshot-entry --name "merged-test"
```

2. Inspect the screenshot and identify the merged-record card bbox. Open and snapshot the popup:

```powershell
& $wxbridge merged open-entry-and-snapshot --snapshot "<captures-dir>\merged-test_assets\merged-entry-snapshot.json" --x 78 --y 310 --w 248 --h 132 --name "merged-test" --index 001
```

3. Inspect each popup screenshot and write lightweight popup analysis:

```json
{
  "snapshot": "<captures-dir>\\merged-test_assets\\merged-popup-snapshot-001.json",
  "copyPoints": [
    { "speaker": "sender-a", "type": "image", "x": 204, "y": 187 },
    { "speaker": "sender-a", "type": "text", "x": 241, "y": 361 }
  ]
}
```

4. Apply, scroll, and snapshot next screen:

```powershell
& $wxbridge merged apply-scroll-snapshot --input "<captures-dir>\merged-test_assets\merged-popup-analysis-001.json" --name "merged-test" --index 002
```

5. Repeat for each new popup screenshot. On the final screen:

```powershell
& $wxbridge merged apply-scroll-snapshot --input "<captures-dir>\merged-test_assets\merged-popup-analysis-004.json" --name "merged-test" --no-scroll
```

The merged exporter keeps `merged-export-state.json` and skips duplicate copied text and duplicate images by SHA256.

## Final Response Format

When an export succeeds, return the full Markdown path in a copyable block:

```powershell
<captures-dir>\merged-test.md
```


