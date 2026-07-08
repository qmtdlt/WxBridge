namespace WxBridge.Windows;

internal static class KeyboardInputDriver
{
    public static void Paste()
    {
        CtrlKey(NativeMethods.VkV);
    }

    public static void SelectAll()
    {
        CtrlKey(NativeMethods.VkA);
    }

    public static void Copy()
    {
        CtrlKey(NativeMethods.VkC);
    }

    public static void Enter()
    {
        NativeMethods.keybd_event(NativeMethods.VkReturn, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkReturn, 0, NativeMethods.KeyEventFKeyUp, UIntPtr.Zero);
    }

    public static void Backspace()
    {
        NativeMethods.keybd_event(NativeMethods.VkBack, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkBack, 0, NativeMethods.KeyEventFKeyUp, UIntPtr.Zero);
    }

    public static void Escape()
    {
        NativeMethods.keybd_event(NativeMethods.VkEscape, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkEscape, 0, NativeMethods.KeyEventFKeyUp, UIntPtr.Zero);
    }

    private static void CtrlKey(byte key)
    {
        NativeMethods.keybd_event(NativeMethods.VkControl, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(key, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(key, 0, NativeMethods.KeyEventFKeyUp, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VkControl, 0, NativeMethods.KeyEventFKeyUp, UIntPtr.Zero);
    }
}
