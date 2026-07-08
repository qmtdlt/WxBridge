using System.Runtime.InteropServices;

namespace WxBridge.Windows;

internal static class MouseInputDriver
{
    public static void Click(int x, int y)
    {
        Click(x, y, 25, 35);
    }

    public static void Click(int x, int y, int moveDelayMs, int holdDelayMs)
    {
        NativeMethods.SetCursorPos(x, y);
        NudgeCursor();
        Thread.Sleep(Math.Max(0, moveDelayMs));

        SendMouseInput(NativeMethods.MouseEventFLeftDown);
        Thread.Sleep(Math.Max(0, holdDelayMs));
        SendMouseInput(NativeMethods.MouseEventFLeftUp);
    }

    public static void RightClick(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
        NudgeCursor();
        Thread.Sleep(25);

        SendMouseInput(NativeMethods.MouseEventFRightDown);
        Thread.Sleep(35);
        SendMouseInput(NativeMethods.MouseEventFRightUp);
    }

    public static void Wheel(int notches)
    {
        if (notches == 0)
        {
            return;
        }

        SendMouseInput(NativeMethods.MouseEventFWheel, 0, 0, -notches * 120);
    }

    public static void WheelRepeated(int notches, int delayMs)
    {
        var steps = Math.Abs(notches);
        if (steps == 0)
        {
            return;
        }

        var step = Math.Sign(notches);
        for (var index = 0; index < steps; index++)
        {
            Wheel(step);
            Thread.Sleep(Math.Max(0, delayMs));
        }
    }

    public static void MoveTo(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
        NudgeCursor();
    }

    private static void SendMouseInput(uint flags)
    {
        SendMouseInput(flags, 0, 0);
    }

    private static void SendMouseInput(uint flags, int dx, int dy)
    {
        SendMouseInput(flags, dx, dy, 0);
    }

    private static void SendMouseInput(uint flags, int dx, int dy, int mouseData)
    {
        var input = MouseInput(flags, dx, dy);
        input.MouseInput.MouseData = unchecked((uint)mouseData);
        var inputs = new[] { input };
        _ = NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    private static void NudgeCursor()
    {
        SendMouseInput(NativeMethods.MouseEventFMove, 0, 1);
        Thread.Sleep(10);
        SendMouseInput(NativeMethods.MouseEventFMove, 0, -1);
    }

    private static NativeMethods.Input MouseInput(uint flags, int dx = 0, int dy = 0)
    {
        return new NativeMethods.Input
        {
            Type = NativeMethods.InputMouse,
            MouseInput = new NativeMethods.MouseInput
            {
                Dx = dx,
                Dy = dy,
                DwFlags = flags
            }
        };
    }
}
