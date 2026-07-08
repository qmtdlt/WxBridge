namespace WxBridge.Core;

public sealed record InspectSessionsRequest(
    int MaxDepth = 6,
    int Limit = 300,
    bool IncludeEmpty = false,
    string View = "control");
