using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MacroCreatorApp
{
    public class MacroPlayer
    {
        // ── WinAPI structs ────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; public static int Size => Marshal.SizeOf<INPUT>(); }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT    { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT    { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

        [DllImport("user32.dll")] static extern uint  SendInput(uint n, INPUT[] p, int sz);
        [DllImport("user32.dll")] static extern short VkKeyScan(char ch);
        [DllImport("user32.dll")] static extern bool  SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern bool  GetCursorPos(out POINT lp);
        [DllImport("user32.dll")] static extern int   GetSystemMetrics(int n);

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

        const uint INPUT_KEYBOARD = 1, INPUT_MOUSE = 0;
        const uint KEYEVENTF_KEYUP       = 0x0002;
        const uint MOUSEEVENTF_MOVE      = 0x0001;
        const uint MOUSEEVENTF_LEFTDOWN  = 0x0002, MOUSEEVENTF_LEFTUP   = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP  = 0x0010;
        const uint MOUSEEVENTF_MIDDOWN   = 0x0020, MOUSEEVENTF_MIDUP    = 0x0040;
        const uint MOUSEEVENTF_XDOWN     = 0x0080, MOUSEEVENTF_XUP      = 0x0100;
        const uint MOUSEEVENTF_WHEEL     = 0x0800;
        const uint MOUSEEVENTF_ABSOLUTE  = 0x8000;
        const uint XBUTTON1 = 1, XBUTTON2 = 2;

        public event Action<int>? StepStarted;
        public event Action?      PlaybackFinished;

        // ── Public API ───────────────────────────────────────────────
        public async Task PlayAsync(IList<MacroStep> steps, float speed, int repeats, bool loop, CancellationToken ct)
        {
            async Task RunOnce()
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (ct.IsCancellationRequested) return;
                    StepStarted?.Invoke(i);
                    int delay = Math.Max(0, (int)(steps[i].DelayBefore / speed));
                    if (delay > 0) { try { await Task.Delay(delay, ct); } catch { return; } }
                    if (ct.IsCancellationRequested) return;
                    ExecuteStep(steps[i], speed);
                }
            }

            if (loop) { while (!ct.IsCancellationRequested) await RunOnce(); }
            else { for (int r = 0; r < repeats && !ct.IsCancellationRequested; r++) await RunOnce(); }

            PlaybackFinished?.Invoke();
        }

        private void ExecuteStep(MacroStep step, float speed)
        {
            switch (step.Type)
            {
                case StepType.Key:        SendKey(step.Key, step.KeyAction); break;
                case StepType.Delay:      Thread.Sleep(Math.Max(0, (int)(step.Duration / speed))); break;
                case StepType.Text:       SendText(step.TextContent); break;
                case StepType.Mouse:      SendMouseClick(step); break;
                case StepType.MouseWheel: SendWheel(step.WheelDelta); break;
            }
        }

        // ── Key ──────────────────────────────────────────────────────
        private void SendKey(string keyName, KeyActionType action)
        {
            var parts  = keyName.Split('+');
            var modVKs = new List<ushort>();
            ushort mainVk = 0;

            foreach (var p in parts)
            {
                ushort vk = GetVK(p.Trim());
                if (vk == 0) continue;
                if (p.Trim() is "Ctrl" or "Alt" or "Shift" or "Win") modVKs.Add(vk);
                else mainVk = vk;
            }
            if (mainVk == 0 && modVKs.Count > 0) mainVk = modVKs[^1];

            var ins = new List<INPUT>();
            foreach (var m in modVKs)                           ins.Add(MakeKey(m, false));
            if (action is KeyActionType.DownUp or KeyActionType.Down) ins.Add(MakeKey(mainVk, false));
            if (action is KeyActionType.DownUp or KeyActionType.Up)   ins.Add(MakeKey(mainVk, true));
            foreach (var m in modVKs)                           ins.Add(MakeKey(m, true));
            SendInput((uint)ins.Count, ins.ToArray(), INPUT.Size);
        }

        private void SendText(string text)
        {
            foreach (char c in text)
            {
                short scan = VkKeyScan(c);
                if (scan == -1) continue;
                ushort vk = (ushort)(scan & 0xFF);
                bool shift = (scan >> 8 & 1) != 0;
                var ins = new List<INPUT>();
                if (shift) ins.Add(MakeKey(0x10, false));
                ins.Add(MakeKey(vk, false)); ins.Add(MakeKey(vk, true));
                if (shift) ins.Add(MakeKey(0x10, true));
                SendInput((uint)ins.Count, ins.ToArray(), INPUT.Size);
                Thread.Sleep(8);
            }
        }

        // ── Mouse ────────────────────────────────────────────────────
        private void SendMouseClick(MacroStep step)
        {
            // Move to absolute position if specified
            if (step.UseAbsolutePosition)
            {
                int sw = GetSystemMetrics(0); // SM_CXSCREEN
                int sh = GetSystemMetrics(1); // SM_CYSCREEN
                int ax = (int)((long)step.MouseX * 65535 / sw);
                int ay = (int)((long)step.MouseY * 65535 / sh);
                var move = new INPUT
                {
                    type = INPUT_MOUSE,
                    U = new InputUnion { mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } }
                };
                SendInput(1, new[] { move }, INPUT.Size);
                Thread.Sleep(15); // small settle time
            }

            (uint down, uint up, uint xBtn) = step.MouseButton switch
            {
                MouseButtonType.Right  => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, 0u),
                MouseButtonType.Middle => (MOUSEEVENTF_MIDDOWN,   MOUSEEVENTF_MIDUP,   0u),
                MouseButtonType.X1     => (MOUSEEVENTF_XDOWN,     MOUSEEVENTF_XUP,     XBUTTON1),
                MouseButtonType.X2     => (MOUSEEVENTF_XDOWN,     MOUSEEVENTF_XUP,     XBUTTON2),
                _                     => (MOUSEEVENTF_LEFTDOWN,   MOUSEEVENTF_LEFTUP,  0u)
            };

            int clicks = step.MouseAction == MouseActionType.DoubleClick ? 2 : 1;
            bool downOnly = step.MouseAction == MouseActionType.Down;
            bool upOnly   = step.MouseAction == MouseActionType.Up;

            for (int i = 0; i < clicks; i++)
            {
                var ins = new List<INPUT>();
                if (!upOnly)   ins.Add(MakeMouse(down, xBtn));
                if (!downOnly) ins.Add(MakeMouse(up,   xBtn));
                SendInput((uint)ins.Count, ins.ToArray(), INPUT.Size);
                if (i < clicks - 1) Thread.Sleep(50);
            }
        }

        private void SendWheel(int delta)
        {
            var inp = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_WHEEL, mouseData = unchecked((uint)delta) } }
            };
            SendInput(1, new[] { inp }, INPUT.Size);
        }

        // ── Helpers ──────────────────────────────────────────────────
        static INPUT MakeKey(ushort vk, bool up) => new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } } };
        static INPUT MakeMouse(uint flags, uint data = 0) => new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, mouseData = data } } };

        private static ushort GetVK(string k) => k switch
        {
            "Space" => 0x20, "Enter" => 0x0D, "Escape" => 0x1B, "Backspace" => 0x08,
            "Tab" => 0x09, "Delete" => 0x2E, "Insert" => 0x2D,
            "Home" => 0x24, "End" => 0x23, "PageUp" => 0x21, "PageDown" => 0x22,
            "↑" => 0x26, "↓" => 0x28, "←" => 0x25, "→" => 0x27,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72,  "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76,  "F8" => 0x77,
            "F9" => 0x78, "F10"=> 0x79, "F11"=> 0x7A,  "F12"=> 0x7B,
            "Ctrl" => 0x11, "Alt" => 0x12, "Shift" => 0x10, "Win" => 0x5B,
            "A" or "a" => 0x41, "B" or "b" => 0x42, "C" or "c" => 0x43, "D" or "d" => 0x44,
            "E" or "e" => 0x45, "F" or "f" => 0x46, "G" or "g" => 0x47, "H" or "h" => 0x48,
            "I" or "i" => 0x49, "J" or "j" => 0x4A, "K" or "k" => 0x4B, "L" or "l" => 0x4C,
            "M" or "m" => 0x4D, "N" or "n" => 0x4E, "O" or "o" => 0x4F, "P" or "p" => 0x50,
            "Q" or "q" => 0x51, "R" or "r" => 0x52, "S" or "s" => 0x53, "T" or "t" => 0x54,
            "U" or "u" => 0x55, "V" or "v" => 0x56, "W" or "w" => 0x57, "X" or "x" => 0x58,
            "Y" or "y" => 0x59, "Z" or "z" => 0x5A,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            "NumPad0" => 0x60, "NumPad1" => 0x61, "NumPad2" => 0x62, "NumPad3" => 0x63, "NumPad4" => 0x64,
            "NumPad5" => 0x65, "NumPad6" => 0x66, "NumPad7" => 0x67, "NumPad8" => 0x68, "NumPad9" => 0x69,
            _ => k.Length == 1 ? (ushort)(VkKeyScan(k[0]) & 0xFF) : (ushort)0
        };
    }
}
