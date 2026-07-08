using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace WxBridge.Windows;

internal static class WindowsClipboard
{
    public static uint GetSequenceNumber()
    {
        return NativeMethods.GetClipboardSequenceNumber();
    }

    public static ClipboardSnapshot Capture()
    {
        var sequence = GetSequenceNumber();
        string? text = null;
        Image? image = null;
        string[]? files = null;

        RunSta(() =>
        {
            if (Clipboard.ContainsText())
            {
                text = Clipboard.GetText();
            }

            if (Clipboard.ContainsImage())
            {
                image = Clipboard.GetImage();
            }

            if (Clipboard.ContainsFileDropList())
            {
                var fileDropList = Clipboard.GetFileDropList();
                files = fileDropList.Cast<string>().ToArray();
            }
        });

        return new ClipboardSnapshot(sequence, text, image, files);
    }

    public static void Restore(ClipboardSnapshot snapshot)
    {
        RunSta(() =>
        {
            var data = new DataObject();
            var hasData = false;

            if (!string.IsNullOrEmpty(snapshot.Text))
            {
                data.SetText(snapshot.Text);
                hasData = true;
            }

            if (snapshot.Image is not null)
            {
                data.SetImage(snapshot.Image);
                hasData = true;
            }

            if (snapshot.Files is { Length: > 0 })
            {
                var fileDropList = new System.Collections.Specialized.StringCollection();
                fileDropList.AddRange(snapshot.Files);
                data.SetFileDropList(fileDropList);
                hasData = true;
            }

            if (hasData)
            {
                Clipboard.SetDataObject(data, true);
            }
            else
            {
                Clipboard.Clear();
            }
        });
    }

    public static string GetText()
    {
        OpenClipboardWithRetry();
        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer) ?? string.Empty;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public static void SetText(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + "\0");
        SetData(NativeMethods.CfUnicodeText, bytes);
    }

    public static void Clear()
    {
        RunSta(Clipboard.Clear);
    }

    public static void SetFileDropList(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var fileList = fullPath + "\0\0";
        var pathBytes = Encoding.Unicode.GetBytes(fileList);
        var bytes = new byte[20 + pathBytes.Length];

        BitConverter.GetBytes(20).CopyTo(bytes, 0);
        BitConverter.GetBytes(1).CopyTo(bytes, 16);
        pathBytes.CopyTo(bytes, 20);

        SetData(NativeMethods.CfHDrop, bytes);
    }

    private static void SetData(uint format, byte[] bytes)
    {
        var hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GmemMoveable, (nuint)bytes.Length);
        if (hGlobal == IntPtr.Zero)
        {
            throw new InvalidOperationException($"GlobalAlloc failed: {Marshal.GetLastWin32Error()}");
        }

        var locked = NativeMethods.GlobalLock(hGlobal);
        if (locked == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(hGlobal);
            throw new InvalidOperationException($"GlobalLock failed: {Marshal.GetLastWin32Error()}");
        }

        Marshal.Copy(bytes, 0, locked, bytes.Length);
        NativeMethods.GlobalUnlock(hGlobal);

        try
        {
            OpenClipboardWithRetry();
            if (!NativeMethods.EmptyClipboard())
            {
                throw new InvalidOperationException($"EmptyClipboard failed: {Marshal.GetLastWin32Error()}");
            }

            if (NativeMethods.SetClipboardData(format, hGlobal) == IntPtr.Zero)
            {
                throw new InvalidOperationException($"SetClipboardData failed: {Marshal.GetLastWin32Error()}");
            }

            hGlobal = IntPtr.Zero;
        }
        finally
        {
            NativeMethods.CloseClipboard();
            if (hGlobal != IntPtr.Zero)
            {
                NativeMethods.GlobalFree(hGlobal);
            }
        }
    }

    private static void OpenClipboardWithRetry()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                return;
            }

            Thread.Sleep(30);
        }

        throw new InvalidOperationException($"OpenClipboard failed: {Marshal.GetLastWin32Error()}");
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }
}

internal sealed record ClipboardSnapshot(uint Sequence, string? Text, Image? Image, string[]? Files) : IDisposable
{
    public void Dispose()
    {
        Image?.Dispose();
    }
}
