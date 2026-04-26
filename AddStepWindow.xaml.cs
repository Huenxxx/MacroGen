using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace MacroCreatorApp
{
    public partial class AddStepWindow : Window
    {
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lp);
        struct POINT { public int X, Y; }

        public MacroStep? Result { get; private set; }
        private bool _capturing;
        private string _capturedKey = "";

        public AddStepWindow() { InitializeComponent(); }

        private void TypeChanged(object sender, RoutedEventArgs e)
        {
            if (PanelKey == null) return;
            PanelKey.Visibility   = RbKey.IsChecked   == true ? Visibility.Visible : Visibility.Collapsed;
            PanelDelay.Visibility = RbDelay.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelText.Visibility  = RbText.IsChecked  == true ? Visibility.Visible : Visibility.Collapsed;
            PanelMouse.Visibility = RbMouse.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelWheel.Visibility = RbWheel.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AbsPos_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelCoords == null) return;
            PanelCoords.Visibility = CbAbsPos.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UseCurrentPos_Click(object sender, RoutedEventArgs e)
        {
            if (GetCursorPos(out var p))
            {
                TbMouseX.Text = p.X.ToString();
                TbMouseY.Text = p.Y.ToString();
            }
        }

        private void KeyCapture_Click(object sender, MouseButtonEventArgs e)
        {
            _capturing = true;
            _capturedKey = "";
            TbKeyCapture.Text = "Pulsa ahora cualquier tecla...";
            TbKeyCapture.Foreground = new SolidColorBrush(Color.FromRgb(14, 99, 156)); // Accent blue
            KeyCaptureBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(14, 99, 156));
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_capturing)
            {
                e.Handled = true;
                _capturing = false;
                bool ctrl  = Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl);
                bool alt   = Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt);
                bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                string baseName = e.Key switch
                {
                    Key.Space    => "Space",    Key.Return   => "Enter",
                    Key.Escape   => "Escape",   Key.Back     => "Backspace",
                    Key.Tab      => "Tab",      Key.Delete   => "Delete",
                    Key.Insert   => "Insert",   Key.Home     => "Home",
                    Key.End      => "End",      Key.PageUp   => "PageUp",
                    Key.PageDown => "PageDown",
                    Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→",
                    Key.F1  => "F1",  Key.F2  => "F2",  Key.F3  => "F3",  Key.F4  => "F4",
                    Key.F5  => "F5",  Key.F6  => "F6",  Key.F7  => "F7",  Key.F8  => "F8",
                    Key.F9  => "F9",  Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
                    Key.LeftCtrl  or Key.RightCtrl  => "Ctrl",
                    Key.LeftShift or Key.RightShift => "Shift",
                    Key.LeftAlt   or Key.RightAlt   => "Alt",
                    Key.LWin      or Key.RWin       => "Win",
                    _ => e.Key.ToString()
                };

                var parts = new System.Collections.Generic.List<string>();
                if (ctrl  && baseName != "Ctrl")  parts.Add("Ctrl");
                if (alt   && baseName != "Alt")   parts.Add("Alt");
                if (shift && baseName != "Shift") parts.Add("Shift");
                parts.Add(baseName);
                _capturedKey = string.Join("+", parts);

                TbKeyCapture.Text = _capturedKey;
                TbKeyCapture.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212));
                KeyCaptureBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green for success
                return;
            }
            base.OnKeyDown(e);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            int delay = int.TryParse(TbDelayBefore.Text, out int d) ? Math.Max(0, d) : 0;

            if (RbKey.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_capturedKey)) { MessageBox.Show("Captura una tecla primero."); return; }
                Result = new MacroStep
                {
                    Type      = StepType.Key,
                    Key       = _capturedKey,
                    KeyAction = RbActDown.IsChecked == true ? KeyActionType.Down
                              : RbActUp.IsChecked   == true ? KeyActionType.Up
                              : KeyActionType.DownUp,
                    DelayBefore = delay
                };
            }
            else if (RbDelay.IsChecked == true)
            {
                int dur = int.TryParse(TbDelayMs.Text, out int v) ? Math.Max(0, v) : 500;
                Result = new MacroStep { Type = StepType.Delay, Duration = dur, DelayBefore = delay };
            }
            else if (RbText.IsChecked == true)
            {
                if (string.IsNullOrEmpty(TbText.Text)) { MessageBox.Show("Escribe algo."); return; }
                Result = new MacroStep { Type = StepType.Text, TextContent = TbText.Text, DelayBefore = delay };
            }
            else if (RbMouse.IsChecked == true)
            {
                bool abs = CbAbsPos.IsChecked == true;
                int mx = int.TryParse(TbMouseX.Text, out int mx2) ? mx2 : 0;
                int my = int.TryParse(TbMouseY.Text, out int my2) ? my2 : 0;
                Result = new MacroStep
                {
                    Type        = StepType.Mouse,
                    MouseButton = RbMR.IsChecked  == true ? MouseButtonType.Right
                                : RbMM.IsChecked  == true ? MouseButtonType.Middle
                                : RbMX1.IsChecked == true ? MouseButtonType.X1
                                : RbMX2.IsChecked == true ? MouseButtonType.X2
                                : MouseButtonType.Left,
                    MouseAction = RbMDouble.IsChecked == true ? MouseActionType.DoubleClick
                                : RbMDown.IsChecked   == true ? MouseActionType.Down
                                : RbMUp.IsChecked     == true ? MouseActionType.Up
                                : MouseActionType.Click,
                    UseAbsolutePosition = abs,
                    MouseX = abs ? mx : 0,
                    MouseY = abs ? my : 0,
                    DelayBefore = delay
                };
            }
            else if (RbWheel.IsChecked == true)
            {
                int amt = int.TryParse(TbWheelAmt.Text, out int w) ? Math.Abs(w) : 120;
                int delta = RbWheelDown.IsChecked == true ? -amt : amt;
                Result = new MacroStep { Type = StepType.MouseWheel, WheelDelta = delta, DelayBefore = delay };
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
