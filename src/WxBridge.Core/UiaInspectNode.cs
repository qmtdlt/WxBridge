namespace WxBridge.Core;

public sealed record UiaInspectNode(
    int Depth,
    string Name,
    string ControlType,
    string AutomationId,
    string ClassName,
    UiaRect? Bounds);
