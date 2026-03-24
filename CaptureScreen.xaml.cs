using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;


namespace ScreenTranslator2
{
    /// <summary>
    /// Interaction logic for CaptureScreen.xaml
    /// </summary>
    public partial class CaptureScreen : Window
    {
        #region P/Invoke Declarations
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, 
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;
        #endregion

        public Rect SelectedRegion { get; private set; }
        public BitmapSource? CapturedBitmap { get; private set; }

        private Point _startPoint;
        private bool _isSelecting;
        private RectangleGeometry? _selectionGeometry;
        private Matrix _dpiMatrix;

        public CaptureScreen()
        {
            InitializeComponent();
            Cursor = Cursors.Cross;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set window to cover entire virtual screen
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            var source = PresentationSource.FromVisual(this);
            _dpiMatrix = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

            var fullScreenGeometry = new RectangleGeometry(new Rect(0, 0, Width, Height));
            _selectionGeometry = new RectangleGeometry(new Rect());

            LayoutRoot.Clip = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                fullScreenGeometry,
                _selectionGeometry
            );
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(this);
            CaptureMouse();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            Point currentPoint = e.GetPosition(this);

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            _selectionGeometry.Rect = new Rect(x, y, width, height);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            ReleaseMouseCapture();
            _isSelecting = false;

            SelectedRegion = _selectionGeometry.Rect;

            if (SelectedRegion is { Width: > 0, Height: > 0 })
            {
                Hide();
                CapturedBitmap = CaptureScreenRegion(SelectedRegion);
            }
            else
            {
                CapturedBitmap = null;
            }

            Close();
        }

        private BitmapSource CaptureScreenRegion(Rect region)
        {
            double absX = SystemParameters.VirtualScreenLeft + region.X;
            double absY = SystemParameters.VirtualScreenTop + region.Y;

            int x = (int)Math.Round(absX * _dpiMatrix.M11);
            int y = (int)Math.Round(absY * _dpiMatrix.M22);
            int width = (int)Math.Round(region.Width * _dpiMatrix.M11);
            int height = (int)Math.Round(region.Height * _dpiMatrix.M22);

            IntPtr hScreenDC = IntPtr.Zero;
            IntPtr hMemoryDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                hScreenDC = GetDC(IntPtr.Zero);
                hMemoryDC = CreateCompatibleDC(hScreenDC);
                hBitmap = CreateCompatibleBitmap(hScreenDC, width, height);
                hOldBitmap = SelectObject(hMemoryDC, hBitmap);

                BitBlt(hMemoryDC, 0, 0, width, height, hScreenDC, x, y, SRCCOPY);

                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error capturing screen: {ex.Message}", "Capture Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            finally
            {
                // Cleanup
                if (hOldBitmap != IntPtr.Zero)
                    SelectObject(hMemoryDC, hOldBitmap);
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
                if (hMemoryDC != IntPtr.Zero)
                    DeleteDC(hMemoryDC);
                if (hScreenDC != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, hScreenDC);
            }
        }
    }
}