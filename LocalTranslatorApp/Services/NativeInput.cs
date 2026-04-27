using System.Runtime.InteropServices;
using System.Windows.Input;

namespace LocalTranslatorApp.Services;

public static class NativeInput
{
    private const uint InputKeyboard = 1;
    private const ushort KeyEventKeyUp = 0x0002;
    private const int SwRestore = 9;
    private const int GuiFocus = 4;

    public readonly record struct FocusTarget(IntPtr WindowHandle, IntPtr FocusedHandle);

    public static IntPtr GetForegroundWindowHandle()
    {
        return GetForegroundWindow();
    }

    public static FocusTarget CaptureForegroundTarget()
    {
        var windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero)
        {
            return new FocusTarget(IntPtr.Zero, IntPtr.Zero);
        }

        var thread = GetWindowThreadProcessId(windowHandle, out _);
        var info = new GuiThreadInfo
        {
            Size = Marshal.SizeOf<GuiThreadInfo>()
        };

        var focusedHandle = GetGUIThreadInfo(thread, ref info) ? info.FocusedWindow : IntPtr.Zero;
        return new FocusTarget(windowHandle, focusedHandle);
    }

    public static bool FocusWindow(FocusTarget target)
    {
        return FocusWindow(target.WindowHandle, target.FocusedHandle);
    }

    public static System.Drawing.Point GetFloatingAnchorPoint(FocusTarget target)
    {
        if (TryGetCaretScreenPoint(target, out var caretPoint))
        {
            return caretPoint;
        }

        return System.Windows.Forms.Cursor.Position;
    }

    private static bool TryGetCaretScreenPoint(FocusTarget target, out System.Drawing.Point point)
    {
        point = default;
        if (target.WindowHandle == IntPtr.Zero || !IsWindow(target.WindowHandle))
        {
            return false;
        }

        var thread = GetWindowThreadProcessId(target.WindowHandle, out _);
        if (thread == 0)
        {
            return false;
        }

        var info = new GuiThreadInfo
        {
            Size = Marshal.SizeOf<GuiThreadInfo>()
        };

        if (!GetGUIThreadInfo(thread, ref info) || info.CaretWindow == IntPtr.Zero)
        {
            return false;
        }

        var nativePoint = new NativePoint
        {
            X = info.CaretRect.Left,
            Y = info.CaretRect.Bottom
        };

        if (!ClientToScreen(info.CaretWindow, ref nativePoint))
        {
            return false;
        }

        point = new System.Drawing.Point(nativePoint.X, nativePoint.Y);
        return true;
    }

    public static bool FocusWindow(IntPtr windowHandle)
    {
        return FocusWindow(windowHandle, IntPtr.Zero);
    }

    private static bool FocusWindow(IntPtr windowHandle, IntPtr focusedHandle)
    {
        if (windowHandle == IntPtr.Zero || !IsWindow(windowHandle))
        {
            return false;
        }

        var foregroundWindow = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThread = GetWindowThreadProcessId(windowHandle, out _);
        var currentThread = GetCurrentThreadId();

        if (foregroundThread != 0)
        {
            AttachThreadInput(currentThread, foregroundThread, true);
        }

        if (targetThread != 0)
        {
            AttachThreadInput(currentThread, targetThread, true);
        }

        ShowWindowAsync(windowHandle, SwRestore);
        BringWindowToTop(windowHandle);
        if (focusedHandle != IntPtr.Zero && IsWindow(focusedHandle))
        {
            SetFocus(focusedHandle);
        }
        else
        {
            SetFocus(windowHandle);
        }

        var focused = SetForegroundWindow(windowHandle);

        if (targetThread != 0)
        {
            AttachThreadInput(currentThread, targetThread, false);
        }

        if (foregroundThread != 0)
        {
            AttachThreadInput(currentThread, foregroundThread, false);
        }

        return focused || GetForegroundWindow() == windowHandle;
    }

    public static bool IsKeyDown(Key key)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    public static void SendCtrlC()
    {
        SendModifiedKey(Key.LeftCtrl, Key.C);
    }

    public static void SendCtrlV()
    {
        SendModifiedKey(Key.LeftCtrl, Key.V);
    }

    private static void SendModifiedKey(Key modifier, Key key)
    {
        var modifierVk = (ushort)KeyInterop.VirtualKeyFromKey(modifier);
        var keyVk = (ushort)KeyInterop.VirtualKeyFromKey(key);
        var inputs = new[]
        {
            KeyboardInput(modifierVk, 0),
            KeyboardInput(keyVk, 0),
            KeyboardInput(keyVk, KeyEventKeyUp),
            KeyboardInput(modifierVk, KeyEventKeyUp)
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyboardInput(ushort virtualKey, ushort flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInputData
                {
                    VirtualKey = virtualKey,
                    Scan = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public int Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusedWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort Scan;
        public ushort Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
