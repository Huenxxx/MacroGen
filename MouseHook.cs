using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MacroCreatorApp
{
    public class MouseHookEventArgs : EventArgs
    {
        public MouseButtonType Button { get; set; }
        public bool IsDown { get; set; }
        public bool IsWheel { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int WheelDelta { get; set; }
    }

    public class LowLevelMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL    = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP   = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP   = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP   = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP   = 0x020C;
        private const int WM_MOUSEWHEEL  = 0x020A;
        private const uint LLMHF_INJECTED = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc cb, IntPtr hMod, uint tid);
        [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hk, int code, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? n);

        private delegate IntPtr LowLevelMouseProc(int code, IntPtr wp, IntPtr lp);
        private LowLevelMouseProc? _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public event EventHandler<MouseHookEventArgs>? MouseEvent;

        public void Install()
        {
            _proc = HookCallback;
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
        }

        public void Uninstall()
        {
            if (_hookID != IntPtr.Zero) { UnhookWindowsHookEx(_hookID); _hookID = IntPtr.Zero; }
        }

        private IntPtr HookCallback(int code, IntPtr wp, IntPtr lp)
        {
            if (code >= 0)
            {
                var s   = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lp);
                int msg = (int)wp;

                // Skip injected input (from our own SendInput during playback)
                if ((s.flags & LLMHF_INJECTED) == 0)
                {
                    bool isWheel = msg == WM_MOUSEWHEEL;
                    bool isBtn   = msg is WM_LBUTTONDOWN or WM_LBUTTONUP or
                                          WM_RBUTTONDOWN or WM_RBUTTONUP or
                                          WM_MBUTTONDOWN or WM_MBUTTONUP or
                                          WM_XBUTTONDOWN or WM_XBUTTONUP;

                    if (isBtn || isWheel)
                    {
                        bool isDown = msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN;
                        int  hiWord = (int)(s.mouseData >> 16);

                        var btn = msg switch
                        {
                            WM_LBUTTONDOWN or WM_LBUTTONUP => MouseButtonType.Left,
                            WM_RBUTTONDOWN or WM_RBUTTONUP => MouseButtonType.Right,
                            WM_MBUTTONDOWN or WM_MBUTTONUP => MouseButtonType.Middle,
                            WM_XBUTTONDOWN or WM_XBUTTONUP => hiWord == 1 ? MouseButtonType.X1 : MouseButtonType.X2,
                            _ => MouseButtonType.Left
                        };

                        MouseEvent?.Invoke(this, new MouseHookEventArgs
                        {
                            Button     = btn,
                            IsDown     = isDown,
                            IsWheel    = isWheel,
                            X          = s.x,
                            Y          = s.y,
                            WheelDelta = isWheel ? (short)hiWord : 0
                        });
                    }
                }
            }
            return CallNextHookEx(_hookID, code, wp, lp);
        }

        public void Dispose() => Uninstall();
    }
}
