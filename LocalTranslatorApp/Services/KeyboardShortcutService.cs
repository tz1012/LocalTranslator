using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using LocalTranslatorApp.Models;

namespace LocalTranslatorApp.Services;

public sealed class KeyboardShortcutService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int LlkhfInjected = 0x10;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;
    private DateTimeOffset _lastSequenceKey = DateTimeOffset.MinValue;
    private int _doubleCopyIntervalMs = 600;
    private string _translateShortcut = "Ctrl+C,C";
    private string _insertShortcut = "Ctrl+Enter";
    private string _pendingSequenceStart = "";

    public KeyboardShortcutService()
    {
        _proc = HookCallback;
    }

    public event EventHandler? TranslateSelectedTextRequested;
    public event EventHandler? InsertTranslationRequested;

    public void Start(AppSettings settings)
    {
        Configure(settings);
        if (_hookId == IntPtr.Zero)
        {
            _hookId = SetHook(_proc);
        }
    }

    public void Configure(AppSettings settings)
    {
        _doubleCopyIntervalMs = Math.Clamp(settings.DoubleCopyIntervalMs, 250, 1500);
        _translateShortcut = NormalizeShortcut(settings.TranslateShortcut, "Ctrl+C,C");
        _insertShortcut = NormalizeShortcut(settings.InsertShortcut, "Ctrl+Enter");
        _pendingSequenceStart = "";
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == WmKeyDown || wParam == WmSysKeyDown))
        {
            var info = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);
            if ((info.Flags & LlkhfInjected) == LlkhfInjected)
            {
                return CallNextHookEx(_hookId, code, wParam, lParam);
            }

            var key = KeyInterop.KeyFromVirtualKey(info.VirtualKeyCode);
            if (IsModifierOnly(key))
            {
                return CallNextHookEx(_hookId, code, wParam, lParam);
            }

            var shortcut = BuildShortcut(key);
            if (MatchesTranslateShortcut(shortcut, key))
            {
                TranslateSelectedTextRequested?.Invoke(this, EventArgs.Empty);
                return (IntPtr)1;
            }
            else if (ShortcutEquals(shortcut, _insertShortcut))
            {
                InsertTranslationRequested?.Invoke(this, EventArgs.Empty);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookId, code, wParam, lParam);
    }

    private bool MatchesTranslateShortcut(string shortcut, Key key)
    {
        var parts = _translateShortcut.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return ShortcutEquals(shortcut, _translateShortcut);
        }

        var now = DateTimeOffset.UtcNow;
        if (ShortcutEquals(shortcut, parts[0]))
        {
            if (_pendingSequenceStart == parts[0] &&
                (now - _lastSequenceKey).TotalMilliseconds <= _doubleCopyIntervalMs &&
                (parts.Length == 2 && KeyName(key) == parts[1] || ShortcutEquals(shortcut, parts[1])))
            {
                _pendingSequenceStart = "";
                return true;
            }

            _pendingSequenceStart = parts[0];
            _lastSequenceKey = now;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_pendingSequenceStart) &&
            (now - _lastSequenceKey).TotalMilliseconds <= _doubleCopyIntervalMs &&
            (KeyName(key) == parts[^1] || ShortcutEquals(shortcut, parts[^1])))
        {
            _pendingSequenceStart = "";
            return true;
        }

        _pendingSequenceStart = "";
        return false;
    }

    private static string BuildShortcut(Key key)
    {
        var parts = new List<string>();
        if (IsPressed(Key.LeftCtrl) || IsPressed(Key.RightCtrl))
        {
            parts.Add("Ctrl");
        }

        if (IsPressed(Key.LeftAlt) || IsPressed(Key.RightAlt))
        {
            parts.Add("Alt");
        }

        if (IsPressed(Key.LeftShift) || IsPressed(Key.RightShift))
        {
            parts.Add("Shift");
        }

        if (IsPressed(Key.LWin) || IsPressed(Key.RWin))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    private static bool ShortcutEquals(string left, string right)
    {
        return string.Equals(NormalizeShortcut(left, ""), NormalizeShortcut(right, ""), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeShortcut(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Replace(" ", "", StringComparison.OrdinalIgnoreCase);
    }

    private static string KeyName(Key key)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Space => "Space",
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => $"Num{(int)(key - Key.NumPad0)}",
            _ => key.ToString()
        };
    }

    private static bool IsModifierOnly(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System;
    }

    private static bool IsPressed(Key key)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = currentModule is null ? IntPtr.Zero : GetModuleHandle(currentModule.ModuleName);
        return SetWindowsHookEx(WhKeyboardLl, proc, moduleHandle, 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookStruct
    {
        public int VirtualKeyCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
