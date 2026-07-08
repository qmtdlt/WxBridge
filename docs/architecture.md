# Architecture

## Projects

- `WxBridge.Cli`: command parsing, JSON output, process exit codes.
- `WxBridge.Core`: stable interfaces and result models.
- `WxBridge.Windows`: Windows-specific WeChat automation.

## Design Rule

CLI commands should call `Core` interfaces only. Concrete Windows automation stays inside `WxBridge.Windows`.

This keeps future HTTP APIs, plugin adapters, and Codex-facing wrappers reusable.
