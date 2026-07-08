using System.Drawing;
using System.Text;

namespace WxBridge.Windows;

internal static class ContextMenuCopyClicker
{
    public static bool RightClickAndClickCopy(
        Point anchor,
        int interactionDelayMs)
    {
        foreach (var probe in ProbeY(anchor))
        {
            var before = SnapshotVisibleWindows();
            MouseInputDriver.RightClick(probe.X, probe.Y);
            Thread.Sleep(Math.Max(0, interactionDelayMs));

            if (TryFindContextMenu(before, probe, out var menuRect))
            {
                var copyPoint = new Point(menuRect.Left + (menuRect.Width / 2), menuRect.Top + 36);
                MouseInputDriver.Click(copyPoint.X, copyPoint.Y);
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Point> ProbeY(Point anchor)
    {
        var xOffsets = new[] { 0, -3, 3 };
        foreach (var xOffset in xOffsets)
        {
            var x = anchor.X + xOffset;
            yield return new Point(x, anchor.Y);

            for (var yOffset = 1; yOffset <= 6; yOffset++)
            {
                yield return new Point(x, anchor.Y - yOffset);
            }

            for (var yOffset = 1; yOffset <= 6; yOffset++)
            {
                yield return new Point(x, anchor.Y + yOffset);
            }
        }
    }

    private static bool TryFindContextMenu(
        IReadOnlyDictionary<IntPtr, WindowSnapshot> before,
        Point anchor,
        out Rectangle menuRect)
    {
        menuRect = Rectangle.Empty;
        var after = SnapshotVisibleWindows();
        var candidates = after.Values
            .Where(window => !before.ContainsKey(window.Handle))
            .Concat(after.Values.Where(IsLikelyContextMenu))
            .Where(window => IsLikelyContextMenu(window) && IsNear(window.Bounds, anchor))
            .OrderBy(window => Distance(window.Bounds, anchor))
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        menuRect = candidates[0].Bounds;
        return true;
    }

    private static Dictionary<IntPtr, WindowSnapshot> SnapshotVisibleWindows()
    {
        var windows = new Dictionary<IntPtr, WindowSnapshot>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return true;
            }

            windows[hWnd] = new WindowSnapshot(hWnd, GetClassName(hWnd), GetTitle(hWnd), bounds);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static bool IsLikelyContextMenu(WindowSnapshot window)
    {
        var bounds = window.Bounds;
        if (bounds.Width is < 80 or > 460 || bounds.Height is < 40 or > 800)
        {
            return false;
        }

        if (window.ClassName.Equals("#32768", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return window.Title.Equals("Weixin", StringComparison.OrdinalIgnoreCase)
            && window.ClassName.Contains("QWindowToolSaveBits", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNear(Rectangle bounds, Point point)
    {
        return Math.Abs((bounds.Left + (bounds.Width / 2)) - point.X) <= 800
            && Math.Abs((bounds.Top + (bounds.Height / 2)) - point.Y) <= 800;
    }

    private static int Distance(Rectangle bounds, Point point)
    {
        return Math.Abs((bounds.Left + (bounds.Width / 2)) - point.X)
            + Math.Abs((bounds.Top + (bounds.Height / 2)) - point.Y);
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        _ = NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var buffer = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private sealed record WindowSnapshot(IntPtr Handle, string ClassName, string Title, Rectangle Bounds);
}
