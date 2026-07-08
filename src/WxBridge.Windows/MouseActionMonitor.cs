namespace WxBridge.Windows;

internal sealed class MouseActionMonitor : IDisposable
{
    private readonly Action<uint> _onRightThenLeft;
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private IntPtr _hook;
    private uint _sequenceBeforeRightClick;
    private bool _rightClickPending;

    public MouseActionMonitor(Action<uint> onRightThenLeft)
    {
        _onRightThenLeft = onRightThenLeft;
        _proc = HookCallback;
    }

    public void Start()
    {
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _proc, IntPtr.Zero, 0);
        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install mouse hook.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message == NativeMethods.WmRButtonUp)
            {
                _sequenceBeforeRightClick = WindowsClipboard.GetSequenceNumber();
                _rightClickPending = true;
            }
            else if (message == NativeMethods.WmLButtonUp && _rightClickPending)
            {
                _rightClickPending = false;
                var sequence = _sequenceBeforeRightClick;
                Task.Run(() => _onRightThenLeft(sequence));
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
