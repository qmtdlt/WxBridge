---
name: wxbridge
description: Use the local WxBridge CLI to operate WeChat from Codex or another AI agent. Use when the user asks to open or switch WeChat chats, send or prepare messages, send clipboard content, set Markdown output paths or file names, export visible WeChat chat records, export merged-forwarded chat-record popups, or save WeChat conversations through deterministic CLI workflows with human confirmation for sending.
---

# WxBridge

## Overview

Use WxBridge as the deterministic control layer for WeChat. Codex handles natural language, visual positioning, and safety checks; the WxBridge CLI handles window switching, right-click copying, clipboard operations, Markdown writing, image saving, scrolling, and de-duplication.

Run commands from the WxBridge repo:

```powershell
cd <repo-dir>
.\wxbridge.ps1 ...
```

Read [references/commands.md](references/commands.md) when you need exact command syntax or an export workflow.

## Core Rules

- Prefer WxBridge CLI commands over Computer Use for supported WeChat operations.
- Never send a WeChat message without explicit user confirmation after the target chat and message/clipboard intent are clear.
- For `sessions open --name`, pass pinyin, not Chinese. Convert names like `file-transfer-assistant` to `filetransferassistant` before calling the CLI.
- For visual exports, Codex should identify sender, type, and screenshot-relative copy points only. Do not OCR message text as the primary source.
- Let the CLI right-click-copy text/images and write Markdown. JSON text is only a fallback if copy fails.
- Skip partially visible messages unless the user explicitly wants them.
- Return the final Markdown full path in a copyable PowerShell code block.

## Common Tasks

### Open Or Switch Chat

Convert the target name to pinyin and run:

```powershell
.\wxbridge.ps1 sessions open --name "pinyin-name"
```

### Send Message Or Clipboard Content

1. Open the target chat with `sessions open`.
2. State the target chat and intended content to the user.
3. Wait for explicit confirmation.
4. Only then run the send/paste command supported by the local CLI.

If the content is already in the clipboard, avoid overwriting the clipboard while switching chats.

### Configure Markdown Output

Use the config commands in [references/commands.md](references/commands.md) to set the default output directory or default Markdown name. Prefer `--name` for ordinary exports after the directory is configured.

### Export Current Visible Chat

Use `messages snapshot-visible`, inspect the screenshot, write lightweight `copyPoints`, then run `messages apply-visible-analysis`.

Each copy point must bind content to the correct speaker:

```json
{ "speaker": "sender-a", "role": "other", "type": "text", "x": 180, "y": 245 }
```

For the user's own messages, use `role: "self"` so WxBridge uses the configured self speaker name.

### Export Merged Chat Record

Use `merged snapshot-entry`, identify the merged-record card, open and snapshot the popup, then iterate popup `copyPoints` plus scroll until no new content appears.

Stop when the screenshot hash does not change or the final apply reports all items as duplicates.

## Safety

- If target chat identity is uncertain, stop before sending and ask the user to confirm.
- If an export command reports failed text/images, inspect the screenshot and retry with better copy points before declaring completion.
- If a WeChat popup/window disappears or the active chat changes unexpectedly, stop and report the issue rather than continuing clicks.


