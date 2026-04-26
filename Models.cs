using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MacroCreatorApp
{
    public enum StepType { Key, Delay, Text, Mouse, MouseWheel }
    public enum KeyActionType { DownUp, Down, Up }
    public enum MouseButtonType { Left, Right, Middle, X1, X2 }
    public enum MouseActionType { Click, DoubleClick, Down, Up }

    public class MacroStep : INotifyPropertyChanged
    {
        private int _delayBefore;

        public StepType Type { get; set; }
        public string Key { get; set; } = "";
        public KeyActionType KeyAction { get; set; } = KeyActionType.DownUp;
        public int Duration { get; set; } = 500;
        public string TextContent { get; set; } = "";
        public MouseButtonType MouseButton { get; set; } = MouseButtonType.Left;
        public MouseActionType MouseAction { get; set; } = MouseActionType.Click;
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool UseAbsolutePosition { get; set; } = true;
        public int WheelDelta { get; set; } = 120;

        public int DelayBefore
        {
            get => _delayBefore;
            set { _delayBefore = value; OnPropertyChanged(nameof(DelayBefore)); OnPropertyChanged(nameof(DelayLabel)); }
        }

        public string TypeIcon => Type switch
        {
            StepType.Key        => "⌨",
            StepType.Delay      => "⏱",
            StepType.Text       => "✏",
            StepType.Mouse      => "🖱",
            StepType.MouseWheel => "⟳",
            _ => "?"
        };

        public string TypeColor => Type switch
        {
            StepType.Key        => "#8B5CF6",
            StepType.Delay      => "#F59E0B",
            StepType.Text       => "#10B981",
            StepType.Mouse      => "#3B82F6",
            StepType.MouseWheel => "#06B6D4",
            _ => "#6B7280"
        };

        public string DisplayLabel => Type switch
        {
            StepType.Key        => $"{KeyActionStr(KeyAction)} [{Key}]",
            StepType.Delay      => $"Esperar {Duration} ms",
            StepType.Text       => $"Escribir: \"{TextContent}\"",
            StepType.Mouse      => $"{MouseActionStr(MouseAction)} {BtnStr(MouseButton)}" + (UseAbsolutePosition ? $" @ ({MouseX},{MouseY})" : ""),
            StepType.MouseWheel => $"Rueda {(WheelDelta > 0 ? "↑ arriba" : "↓ abajo")} ({Math.Abs(WheelDelta)})",
            _ => "Desconocido"
        };

        public string DelayLabel => $"+{DelayBefore} ms";

        private static string KeyActionStr(KeyActionType a) => a switch { KeyActionType.Down => "Bajar", KeyActionType.Up => "Soltar", _ => "Pulsar" };
        private static string MouseActionStr(MouseActionType a) => a switch { MouseActionType.DoubleClick => "Doble Clic", MouseActionType.Down => "Bajar", MouseActionType.Up => "Soltar", _ => "Clic" };
        private static string BtnStr(MouseButtonType b) => b switch { MouseButtonType.Right => "Derecho", MouseButtonType.Middle => "Central", MouseButtonType.X1 => "Lateral 1", MouseButtonType.X2 => "Lateral 2", _ => "Izquierdo" };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Macro : INotifyPropertyChanged
    {
        private string _name = "Macro";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(DisplayName)); } }
        public ObservableCollection<MacroStep> Steps { get; set; } = new();
        public string DisplayName => $"{_name}  ({Steps.Count} pasos)";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
