using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;


namespace MacroCreatorApp
{
    public partial class MainWindow : Window
    {
        // ── State ─────────────────────────────────────────────────────
        private readonly ObservableCollection<Macro> _macros = new();
        private Macro? ActiveMacro => MacroListBox.SelectedItem as Macro;

        private bool _isRecording;
        private bool _isPlaying;
        private bool _ignoreHotkeys;

        private readonly LowLevelKeyboardHook _keyHook   = new();
        private readonly LowLevelMouseHook    _mouseHook = new();
        private readonly MacroPlayer          _player    = new();
        private CancellationTokenSource? _playCts;

        private readonly Stopwatch _recClock = new();
        private long _lastEventTick;

        private static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 1.5f, 2f, 5f };
        private readonly DispatcherTimer _recTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };

        // ── Init ──────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            MacroListBox.ItemsSource = _macros;

            _keyHook.KeyEvent        += OnKeyEvent;
            _mouseHook.MouseEvent    += OnMouseEvent;
            _player.StepStarted      += OnStepStarted;
            _player.PlaybackFinished += OnPlaybackFinished;
            _recTimer.Tick           += RecTimer_Tick;

            _keyHook.Install(); // Always on for hotkeys
            _mouseHook.Install();

            AddMacro("Macro 1");
            SetStatus("Listo", "#4CAF50", "Pulsa REC para grabar o Añadir para insertar pasos manualmente.");

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";

            // Check for updates in the background
            this.Loaded += async (_, _) => await GitHubUpdater.CheckForUpdatesAsync();
        }

        // ── Macro management ─────────────────────────────────────────
        private void AddMacro(string name)
        {
            var m = new Macro { Name = name };
            m.Steps.CollectionChanged += (_, __) =>
            {
                m.OnPropertyChanged("DisplayName");
                RefreshSteps();
                UpdateButtons();
            };
            _macros.Add(m);
            MacroListBox.SelectedItem = m;
        }

        private void NewMacro_Click(object sender, RoutedEventArgs e)
        {
            string name = $"Macro {_macros.Count + 1}";
            var win = new Window
            {
                Title = "Nueva Macro — MacroGen", Width = 340, Height = 160,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize
            };
            var sp  = new StackPanel { Margin = new Thickness(20) };
            var lbl = new TextBlock { Text = "Nombre:", Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Margin = new Thickness(0, 0, 0, 6) };
            var tb  = new TextBox { Text = name,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Padding = new Thickness(8, 6, 8, 6) };
            var row    = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var okBtn  = new Button { Content = "Crear",    Background = new SolidColorBrush(Color.FromRgb(14, 99, 156)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
            var canBtn = new Button { Content = "Cancelar", Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),   Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(12, 6, 12, 6) };
            bool ok = false;
            okBtn.Click  += (_, __) => { ok = true; win.Close(); };
            canBtn.Click += (_, __) => win.Close();
            tb.KeyDown   += (_, ke) => { if (ke.Key == System.Windows.Input.Key.Return) { ok = true; win.Close(); } };
            row.Children.Add(okBtn); row.Children.Add(canBtn);
            sp.Children.Add(lbl); sp.Children.Add(tb); sp.Children.Add(row);
            win.Content = sp;
            win.Loaded += (_, __) => { tb.SelectAll(); tb.Focus(); };
            win.ShowDialog();
            if (ok) AddMacro(string.IsNullOrWhiteSpace(tb.Text) ? name : tb.Text.Trim());
        }

        private void DeleteMacro_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveMacro == null || _isRecording || _isPlaying) return;
            if (MessageBox.Show($"¿Eliminar \"{ActiveMacro.Name}\"?", "MacroGen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _macros.Remove(ActiveMacro);
            if (_macros.Count > 0) MacroListBox.SelectedIndex = 0;
            RefreshSteps(); UpdateButtons();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording || _isPlaying) return;
            if (ActiveMacro == null) { MessageBox.Show("No hay ninguna macro seleccionada para guardar."); return; }

            var dlg = new SaveFileDialog { Filter = "Archivos MacroGen (*.mgen)|*.mgen", DefaultExt = ".mgen", FileName = ActiveMacro.Name };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ActiveMacro.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                    string json = JsonSerializer.Serialize(ActiveMacro, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                    SetStatus("Listo", "#4CAF50", "Macro guardada correctamente.");
                }
                catch (Exception ex) { MessageBox.Show("Error al guardar: " + ex.Message); }
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording || _isPlaying) return;

            var dlg = new OpenFileDialog { Filter = "Archivos MacroGen (*.mgen)|*.mgen" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var macro = JsonSerializer.Deserialize<Macro>(json);
                    if (macro != null)
                    {
                        macro.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                        macro.Steps.CollectionChanged += (_, __) =>
                        {
                            macro.OnPropertyChanged("DisplayName");
                            RefreshSteps(); UpdateButtons();
                        };
                        _macros.Add(macro);
                        MacroListBox.SelectedItem = macro;
                        SetStatus("Listo", "#4CAF50", "Macro importada correctamente.");
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error al importar: " + ex.Message); }
            }
        }

        private void MacroListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { RefreshSteps(); UpdateButtons(); }

        private void MacroListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                e.Handled = true;
                DeleteMacro_Click(this, new RoutedEventArgs());
            }
        }

        // ── Steps ────────────────────────────────────────────────────
        private void RefreshSteps() => StepsListView.ItemsSource = ActiveMacro?.Steps;

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveMacro == null) { MessageBox.Show("Crea una macro primero."); return; }
            _ignoreHotkeys = true;
            var dlg = new AddStepWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
                ActiveMacro.Steps.Add(dlg.Result);
            _ignoreHotkeys = false;
        }

        private void DeleteStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MacroStep step && ActiveMacro != null)
                ActiveMacro.Steps.Remove(step);
        }

        private void StepsListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete && ActiveMacro != null)
            {
                var selectedSteps = StepsListView.SelectedItems.Cast<MacroStep>().ToList();
                if (selectedSteps.Count > 0)
                {
                    e.Handled = true;
                    foreach (var s in selectedSteps) ActiveMacro.Steps.Remove(s);
                }
            }
        }

        private void ClearSteps_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveMacro == null || ActiveMacro.Steps.Count == 0) return;
            if (MessageBox.Show("¿Borrar todos los pasos?", "MacroGen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                ActiveMacro.Steps.Clear();
        }

        // ── Recording ─────────────────────────────────────────────────
        private void RecBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying) return;
            if (_isRecording) StopRecording(); else StartRecording();
        }

        private void StartRecording()
        {
            if (ActiveMacro == null) AddMacro("Macro 1");
            _isRecording = true;
            _lastEventTick = 0;
            _recClock.Restart();
            _recTimer.Start();
            RecordingBanner.Visibility = Visibility.Visible;
            SetStatus("Grabando...", "#F44336", "Grabando — pulsa teclas y clics. Pulsa STOP o REC para terminar.");
            UpdateButtons();
        }

        private void StopRecording()
        {
            _isRecording = false;
            _recTimer.Stop();
            _recClock.Stop();
            RecordingBanner.Visibility = Visibility.Collapsed;
            SetStatus("Listo", "#4CAF50", $"Grabación terminada — {ActiveMacro?.Steps.Count ?? 0} pasos.");
            UpdateButtons();
        }

        private void OnKeyEvent(object? sender, KeyboardHookEventArgs e)
        {
            if (e.Key is "F1" or "F2" or "F3" && !_ignoreHotkeys)
            {
                e.Handled = true;
                if (e.IsKeyDown)
                {
                    if (e.Key == "F1") Dispatcher.Invoke(() => RecBtn_Click(this, new RoutedEventArgs()));
                    else if (e.Key == "F2") Dispatcher.Invoke(() => PlayBtn_Click(this, new RoutedEventArgs()));
                    else if (e.Key == "F3") Dispatcher.Invoke(() => StopBtn_Click(this, new RoutedEventArgs()));
                }
                return;
            }

            if (!_isRecording || ActiveMacro == null) return;
            long now = _recClock.ElapsedMilliseconds;
            int delay = _lastEventTick == 0 ? 0 : (int)(now - _lastEventTick);
            _lastEventTick = now;
            var step = new MacroStep
            {
                Type = StepType.Key,
                Key = e.Key,
                KeyAction = e.IsKeyDown ? KeyActionType.Down : KeyActionType.Up,
                DelayBefore = delay
            };
            Dispatcher.Invoke(() => ActiveMacro.Steps.Add(step));
        }

        private void OnMouseEvent(object? sender, MouseHookEventArgs e)
        {
            if (!_isRecording || ActiveMacro == null) return;
            long now = _recClock.ElapsedMilliseconds;
            int delay = _lastEventTick == 0 ? 0 : (int)(now - _lastEventTick);
            _lastEventTick = now;

            MacroStep step;
            if (e.IsWheel)
            {
                step = new MacroStep { Type = StepType.MouseWheel, WheelDelta = e.WheelDelta, DelayBefore = delay };
            }
            else
            {
                step = new MacroStep
                {
                    Type = StepType.Mouse,
                    MouseButton = e.Button,
                    MouseAction = e.IsDown ? MouseActionType.Down : MouseActionType.Up,
                    MouseX = e.X, MouseY = e.Y,
                    UseAbsolutePosition = true,
                    DelayBefore = delay
                };
            }
            Dispatcher.Invoke(() => ActiveMacro.Steps.Add(step));
        }

        private void RecTimer_Tick(object? sender, EventArgs e)
        {
            long ms = _recClock.ElapsedMilliseconds;
            RecTimerLabel.Text = $"GRABANDO  {ms / 60000:00}:{(ms % 60000) / 1000:00}.{ms % 1000:000}";
            RecStepCount.Text  = $"— {ActiveMacro?.Steps.Count ?? 0} pasos";
        }

        // ── Playback ──────────────────────────────────────────────────
        private async void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying || _isRecording || ActiveMacro == null || ActiveMacro.Steps.Count == 0) return;

            // Countdown so user can switch to target window
            int startDelay = int.TryParse(StartDelayBox.Text, out int sd) ? Math.Max(0, sd) : 0;
            if (startDelay > 0)
            {
                SetStatus($"Iniciando en {startDelay}s...", "#F59E0B", $"Cambia a la ventana objetivo. Empezando en {startDelay} segundos...");
                for (int i = startDelay; i > 0; i--)
                {
                    FooterText.Text = $"⏳  Iniciando en {i} segundo{(i != 1 ? "s" : "")}...  (cambia de ventana ahora)";
                    await Task.Delay(1000);
                }
            }

            _isPlaying = true;
            _playCts   = new CancellationTokenSource();
            float speed   = Speeds[Math.Max(0, SpeedCombo.SelectedIndex)];
            int   repeats = int.TryParse(RepeatBox.Text, out int r) ? Math.Max(1, r) : 1;
            bool  loop    = LoopToggle.IsChecked == true;

            ProgressPanel.Visibility = Visibility.Visible;
            SetStatus("Reproduciendo...", "#0E639C", "Reproduciendo macro. Pulsa STOP para detener.");
            UpdateButtons();

            var steps = ActiveMacro.Steps.ToList();
            await Task.Run(async () => await _player.PlayAsync(steps, speed, repeats, loop, _playCts.Token));
        }

        private void OnStepStarted(int index)
        {
            var steps = ActiveMacro?.Steps;
            if (steps == null) return;
            Dispatcher.Invoke(() =>
            {
                double pct   = (double)(index + 1) / steps.Count;
                double totalW = (ProgressFill.Parent as Border)?.ActualWidth ?? 0;
                ProgressFill.Width  = totalW * pct;
                ProgressLabel.Text  = $"{index + 1} / {steps.Count}";
                StepsListView.SelectedIndex = index;
                StepsListView.ScrollIntoView(StepsListView.SelectedItem);
            });
        }

        private void OnPlaybackFinished()
        {
            Dispatcher.Invoke(() =>
            {
                _isPlaying = false;
                ProgressPanel.Visibility    = Visibility.Collapsed;
                StepsListView.SelectedIndex = -1;
                SetStatus("Listo", "#4CAF50", "Reproducción completada.");
                UpdateButtons();
            });
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording) StopRecording();
            if (_isPlaying)
            {
                _playCts?.Cancel();
                _isPlaying = false;
                ProgressPanel.Visibility    = Visibility.Collapsed;
                StepsListView.SelectedIndex = -1;
                SetStatus("Listo", "#4CAF50", "Detenido.");
                UpdateButtons();
            }
        }

        // ── UI helpers ────────────────────────────────────────────────
        private void UpdateButtons()
        {
            bool hasSteps = ActiveMacro?.Steps.Count > 0;
            PlayBtn.IsEnabled = !_isRecording && !_isPlaying && hasSteps;
            StopBtn.IsEnabled = _isRecording || _isPlaying;
            RecBtn.IsEnabled  = !_isPlaying;
        }

        private void SetStatus(string label, string dotHex, string footer)
        {
            StatusDot.Fill  = (SolidColorBrush)new BrushConverter().ConvertFromString(dotHex)!;
            StatusText.Text = label;
            FooterText.Text = footer;
        }

        protected override void OnClosed(EventArgs e)
        {
            _keyHook.Dispose(); _mouseHook.Dispose(); _playCts?.Cancel();
            base.OnClosed(e);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        { _keyHook.Dispose(); _mouseHook.Dispose(); _playCts?.Cancel(); }
    }
}