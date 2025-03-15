using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BasedAutoClicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int _defaultHoldTime = 1000;
        private const int _defaultDelay = 100;
        private const int HOTKEY_ID = 9000;
        private const int WM_HOTKEY = 0x0312;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private CancellationTokenSource? _cts;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            RegisterHotKey(helper.Handle, HOTKEY_ID, 0, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.F6));
            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
        }

        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
            {
                handled = true;
                ToggleAutoClick();
            }
        }

        private void ToggleAutoClick()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
                UpdateStatus("Not running", Brushes.Red);
            }
            else
            {
                if (!int.TryParse(txtHoldTime.Text, out int interval) || interval < 1)
                {
                    interval = _defaultHoldTime;
                    UpdateTextBoxValueSafe(interval.ToString(), txtHoldTime);
                }
                if (!int.TryParse(txtDelay.Text, out int delay) || interval < 1)
                {
                    delay = _defaultDelay;
                    UpdateTextBoxValueSafe(delay.ToString(), txtDelay);
                }
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                if(rbHold.IsChecked == true)
                    Task.Run(async () => await AutoClickCycleAsync(interval, delay, token), token);
                if(rbSpam.IsChecked == true)
                    Task.Run(async () => await AutoClickCycleAsync(1, delay, token), token);
                UpdateStatus("RUNNING", Brushes.Green);
            }
        }

        private void UpdateTextBoxValueSafe(string value, TextBox textBox)
        {
            Dispatcher.Invoke(() =>
            {
                textBox.Text = value;
            });
        }

        private void UpdateStatus(string text, Brush color)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = text;
                txtStatus.Foreground = color;
            });
        }

        private async Task AutoClickCycleAsync(int interval, int delay, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(interval, token);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(delay, token);
                }
            }
            catch (OperationCanceledException)
            {
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ToggleAutoClick();
        }
    }
}