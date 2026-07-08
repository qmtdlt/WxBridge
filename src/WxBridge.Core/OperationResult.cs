namespace WxBridge.Core;

public sealed record OperationResult(
    bool Ok,
    string Action,
    string? Error = null,
    object? Data = null)
{
    public static OperationResult Success(string action, object? data = null)
    {
        return new OperationResult(true, action, Data: data);
    }

    public static OperationResult Failure(string action, string error, object? data = null)
    {
        return new OperationResult(false, action, error, data);
    }
}
