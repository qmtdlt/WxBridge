# Interfaces

## IChatSessionService

```csharp
OperationResult ListSessions();
OperationResult SwitchSession(SwitchSessionRequest request);
```

## IMessageService

```csharp
OperationResult SendText(SendTextRequest request);
OperationResult SendFile(SendFileRequest request);
OperationResult SendClipboard();
```

## Result Format

```json
{
  "ok": true,
  "action": "sessions.switch",
  "error": null,
  "data": {
    "index": 1
  }
}
```

Codex should treat `ok: false` as a failed operation and inspect `error`.
