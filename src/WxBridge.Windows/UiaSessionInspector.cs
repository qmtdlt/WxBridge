using System.Runtime.InteropServices;
using Interop.UIAutomationClient;
using WxBridge.Core;

namespace WxBridge.Windows;

internal sealed class UiaSessionInspector
{
    private readonly WeChatWindowLocator _windowLocator;

    public UiaSessionInspector(WeChatOptions options)
    {
        _windowLocator = new WeChatWindowLocator(options);
    }

    public OperationResult Inspect(InspectSessionsRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("sessions.inspect", "windows_required");
        }

        var hWnd = _windowLocator.FindMainWindow();
        if (hWnd == IntPtr.Zero)
        {
            return OperationResult.Failure("sessions.inspect", "wechat_window_not_found");
        }

        IUIAutomation? automation = null;
        IUIAutomationElement? root = null;

        try
        {
            automation = new CUIAutomation();
            root = automation.ElementFromHandle(hWnd);
            var walker = SelectWalker(automation, request.View);
            var nodes = new List<UiaInspectNode>();

            Walk(root, walker, 0, request, nodes);

            return OperationResult.Success(
                "sessions.inspect",
                new
                {
                    maxDepth = request.MaxDepth,
                    limit = request.Limit,
                    includeEmpty = request.IncludeEmpty,
                    view = request.View,
                    count = nodes.Count,
                    nodes
                });
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("sessions.inspect", "uia_inspect_failed", new { ex.Message });
        }
        finally
        {
            ReleaseComObject(root);
            ReleaseComObject(automation);
        }
    }

    private static IUIAutomationTreeWalker SelectWalker(IUIAutomation automation, string view)
    {
        return view.ToLowerInvariant() switch
        {
            "raw" => automation.RawViewWalker,
            "content" => automation.ContentViewWalker,
            _ => automation.ControlViewWalker
        };
    }

    private static void Walk(IUIAutomationElement element, IUIAutomationTreeWalker walker, int depth, InspectSessionsRequest request, List<UiaInspectNode> nodes)
    {
        if (nodes.Count >= request.Limit || depth > request.MaxDepth)
        {
            return;
        }

        var node = ReadNode(element, depth);
        if (request.IncludeEmpty || !string.IsNullOrWhiteSpace(node.Name) || !string.IsNullOrWhiteSpace(node.AutomationId))
        {
            nodes.Add(node);
        }

        if (nodes.Count >= request.Limit || depth == request.MaxDepth)
        {
            return;
        }

        IUIAutomationElement? child = null;
        try
        {
            child = walker.GetFirstChildElement(element);
            while (child is not null && nodes.Count < request.Limit)
            {
                Walk(child, walker, depth + 1, request, nodes);
                var current = child;
                child = walker.GetNextSiblingElement(child);
                ReleaseComObject(current);
            }
        }
        finally
        {
            ReleaseComObject(child);
        }
    }

    private static UiaInspectNode ReadNode(IUIAutomationElement element, int depth)
    {
        var controlType = ReadInt(() => element.CurrentControlType);
        return new UiaInspectNode(
            depth,
            ReadString(() => element.CurrentName),
            UiaControlTypes.GetName(controlType),
            ReadString(() => element.CurrentAutomationId),
            ReadString(() => element.CurrentClassName),
            ReadBounds(element));
    }

    private static UiaRect? ReadBounds(IUIAutomationElement element)
    {
        try
        {
            var rect = element.CurrentBoundingRectangle;
            return new UiaRect((int)rect.left, (int)rect.top, (int)rect.right, (int)rect.bottom);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadString(Func<string> read)
    {
        try
        {
            return Convert.ToString(read()) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int ReadInt(Func<int> read)
    {
        try
        {
            return Convert.ToInt32(read());
        }
        catch
        {
            return 0;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (OperatingSystem.IsWindows() && value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }
}
