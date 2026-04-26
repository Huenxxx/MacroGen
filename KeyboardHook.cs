using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace MacroCreatorApp
{
    public class KeyboardHookEventArgs : EventArgs
    {
        public string Key { get; set; } = "";
        public bool IsKeyDown { get; set; }
        public bool Handled { get; set; }
    }

    public class LowLevelKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_KEYUP      = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP   = 0x0105;

        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc cb, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hk, int code, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? name);
        [DllImport("user32.dll")] static extern short GetKeyState(int vk);

        private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wp, IntPtr lp);

        private LowLevelKeyboardProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public event EventHandler<KeyboardHookEventArgs>? KeyEvent;

        public void Install()
        {
            _proc = HookCallback;
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
        }

        public void Uninstall()
        {
            if (_hookID != IntPtr.Zero) { UnhookWindowsHookEx(_hookID); _hookID = IntPtr.Zero; }
        }

        private IntPtr HookCallback(int code, IntPtr wp, IntPtr lp)
        {
            if (code >= 0)
            {
                int vk = Marshal.ReadInt32(lp);
                bool down = wp == WM_KEYDOWN || wp == WM_SYSKEYDOWN;
                bool up   = wp == WM_KEYUP   || wp == WM_SYSKEYUP;

                if (down || up)
                {
                    bool ctrl  = (GetKeyState(0x11) & 0x8000) != 0;
                    bool alt   = (GetKeyState(0x12) & 0x8000) != 0;
                    bool shift = (GetKeyState(0x10) & 0x8000) != 0;

                    var key = KeyInterop.KeyFromVirtualKey(vk);
                    string name = BuildKeyName(key, shift, ctrl, alt);

                    var args = new KeyboardHookEventArgs { Key = name, IsKeyDown = down };
                    KeyEvent?.Invoke(this, args);
                    if (args.Handled) return new IntPtr(1);
                }
            }
            return CallNextHookEx(_hookID, code, wp, lp);
        }

        private static string BuildKeyName(Key key, bool shift, bool ctrl, bool alt)
        {
            // Skip lone modifier keys
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                     or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return key switch
                {
                    Key.LeftCtrl or Key.RightCtrl   => "Ctrl",
                    Key.LeftShift or Key.RightShift => "Shift",
                    Key.LeftAlt or Key.RightAlt     => "Alt",
                    _ => "Win"
                };

            string baseName = key switch
            {
                Key.Space    => "Space",
                Key.Return   => "Enter",
                Key.Escape   => "Escape",
                Key.Back     => "Backspace",
                Key.Tab      => "Tab",
                Key.Delete   => "Delete",
                Key.Insert   => "Insert",
                Key.Home     => "Home",
                Key.End      => "End",
                Key.PageUp   => "PageUp",
                Key.PageDown => "PageDown",
                Key.Up       => "↑",
                Key.Down     => "↓",
                Key.Left     => "←",
                Key.Right    => "→",
                Key.F1  => "F1",  Key.F2  => "F2",  Key.F3  => "F3",  Key.F4  => "F4",
                Key.F5  => "F5",  Key.F6  => "F6",  Key.F7  => "F7",  Key.F8  => "F8",
                Key.F9  => "F9",  Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
                Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
                Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
                Key.NumPad0 => "NumPad0", Key.NumPad1 => "NumPad1", Key.NumPad2 => "NumPad2", Key.NumPad3 => "NumPad3", Key.NumPad4 => "NumPad4",
                Key.NumPad5 => "NumPad5", Key.NumPad6 => "NumPad6", Key.NumPad7 => "NumPad7", Key.NumPad8 => "NumPad8", Key.NumPad9 => "NumPad9",
                _ => key.ToString().Length == 1
                        ? (shift ? key.ToString().ToUpper() : key.ToString().ToLower())
                        : key.ToString()
            };

            var mods = new System.Collections.Generic.List<string>();
            if (ctrl)  mods.Add("Ctrl");
            if (alt)   mods.Add("Alt");
            if (shift && key.ToString().Length != 1) mods.Add("Shift");
            mods.Add(baseName);
            return string.Join("+", mods);
        }

        public void Dispose() => Uninstall();
    }
}
