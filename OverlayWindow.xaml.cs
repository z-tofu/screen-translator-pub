using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ScreenTranslator2
{
    public partial class OverlayWindow : Window
    {
        private string _position;
        private DispatcherTimer _topmostTimer;

        // Win32 extended window style constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_LAYERED = 0x00080000;

        // SetWindowPos constants
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        public OverlayWindow(string translation, string position = "Top Left")
        {
            InitializeComponent();
            TranslationText.Text = translation;
            _position = position;
            Opacity = 0;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW |
                WS_EX_NOACTIVATE | WS_EX_LAYERED);

            ForceTopmost(hwnd);

            _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _topmostTimer.Tick += (s, _) => ForceTopmost(new WindowInteropHelper(this).Handle);
            _topmostTimer.Start();
        }

        private static void ForceTopmost(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;

            Left = _position switch
            {
                "Top Right" => area.Right - ActualWidth - 20,
                "Bottom Right" => area.Right - ActualWidth - 20,
                _ => area.Left + 20
            };
            Top = _position switch
            {
                "Bottom Left" => area.Bottom - ActualHeight - 40,
                "Bottom Right" => area.Bottom - ActualHeight - 40,
                _ => area.Top + 20
            };

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.4));
            BeginAnimation(OpacityProperty, fadeIn);

            await Task.Delay(8000);

            _topmostTimer?.Stop();

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.6));
            fadeOut.Completed += (s, a) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}