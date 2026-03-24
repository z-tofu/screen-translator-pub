using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;

namespace ScreenTranslator2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001;

        // Modifiers
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Virtual Key Codes
        private const uint VK_Q = 0x51;

        #endregion

        private HwndSource _source;
        private IntPtr _windowHandle;
        private bool _isCaptureWindowOpen;
        private EmbeddedTranslator _translator;

        public MainWindow()
        {
            InitializeComponent();

            _translator = new EmbeddedTranslator();

            SourceInitialized += MainWindow_SourceInitialized;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
        }

        private bool _forceClose = false;

        public void ForceClose()
        {
            _forceClose = true;
            Close();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceClose)
            {
                e.Cancel = true;
                Hide();
            }
        }

        #region Custom Title Bar

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        #endregion

        #region Hotkey Management

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(WndProc);

            // Register Win + Shift + Q hotkey
            if (!RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_WIN | MOD_SHIFT, VK_Q))
            {
                System.Windows.MessageBox.Show(
                    "Failed to register global hotkey (Win+Shift+Q). " +
                    "It might already be in use by another application.",
                    "Hotkey Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _source?.RemoveHook(WndProc);

            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
            }

            _translator?.Dispose();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OpenCaptureWindow();
                handled = true;
            }

            return IntPtr.Zero;
        }

        #endregion

        #region Capture Logic

        private void SnipButton_Click(object sender, RoutedEventArgs e)
        {
            OpenCaptureWindow();
        }

        private async void OpenCaptureWindow()
        {
            if (_isCaptureWindowOpen)
                return;

            _isCaptureWindowOpen = true;

            try
            {
                if (IsVisible)
                {
                    WindowState = WindowState.Minimized;
                    await Task.Delay(150);
                }

                var captureWindow = new CaptureScreen();
                captureWindow.ShowDialog();

                if (IsVisible)
                {
                    WindowState = WindowState.Normal;
                    Activate();
                }

                if (captureWindow.CapturedBitmap != null)
                {
                    CapturedImageView.Source = captureWindow.CapturedBitmap;

                    // Perform OCR on captured image
                    await PerformOcrAndTranslateAsync(captureWindow.CapturedBitmap);
                }
            }
            finally
            {
                _isCaptureWindowOpen = false;
            }
        }

        #endregion

        #region OCR Processing

        private async Task PerformOcrAndTranslateAsync(BitmapSource bitmapSource)
        {
            ResultTextBox.Text = "Processing image...";
            TranslationBox.Text = "Waiting for OCR...";

            try
            {
                string sourceLang = ((System.Windows.Controls.ComboBoxItem)SourceLangComboBox.SelectedItem).Content
                    .ToString();

                string extractedText = await ExtractTextFromBitmapSourceAsync(bitmapSource, sourceLang);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    ResultTextBox.Text = "No text detected in the captured image.";
                    TranslationBox.Text = "";
                    return;
                }

                ResultTextBox.Text = extractedText;

                await TranslateTextAsync(extractedText);
            }
            catch (Exception ex)
            {
                ResultTextBox.Text = $"Error during OCR: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"OCR processing failed: {ex.Message}",
                    "OCR Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task TranslateTextAsync(string textToTranslate)
        {
            TranslationBox.Text = "Translating...";

            try
            {
                string sourceLang = ((System.Windows.Controls.ComboBoxItem)SourceLangComboBox.SelectedItem).Content
                    .ToString();
                string targetLang = ((System.Windows.Controls.ComboBoxItem)TargetLangComboBox.SelectedItem).Content
                    .ToString();
                string modelSelection =
                    ((System.Windows.Controls.ComboBoxItem)ModelComboBox.SelectedItem).Content.ToString();

                string modelFileName = modelSelection.Contains("Phi-3") ? "phi3-mini.gguf" : "mistral-v0.3.gguf";
                string modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                string modelPath = Path.Combine(modelsDir, modelFileName);

                if (!File.Exists(modelPath))
                {
                    TranslationBox.Text =
                        $"Error: Model file not found.\nExpected at: {modelPath}\n\nPlease create the 'models' folder and place the .gguf file there.";
                    return;
                }

                TranslationBox.Text = $"Loading model ({modelFileName})... this may take a few seconds on first run.";
                await _translator.LoadModelAsync(modelPath);

                TranslationBox.Text = "Model loaded. Generating translation...";
                string translation = await _translator.TranslateAsync(textToTranslate, sourceLang, targetLang);

                TranslationBox.Text = translation;

                if (!IsVisible || WindowState == WindowState.Minimized)
                {
                    string overlayPosition =
                        ((System.Windows.Controls.ComboBoxItem)OverlayPositionComboBox.SelectedItem)?.Content
                        ?.ToString() ?? "Top Left";
                    var overlay = new OverlayWindow(translation, overlayPosition);
                    overlay.Show();
                }
            }
            catch (Exception ex)
            {
                TranslationBox.Text = $"Error during translation: {ex.Message}";
            }
        }

        private async Task<string> ExtractTextFromBitmapSourceAsync(BitmapSource bitmapSource, string sourceLangHint)
        {
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;

                var randomAccessStream = memoryStream.AsRandomAccessStream();

                // Decode using WinRT APIs
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                OcrEngine? ocrEngine = null;

                if (sourceLangHint == "Detect language")
                {
                    var lang = new Windows.Globalization.Language("en-US");
                    if (OcrEngine.IsLanguageSupported(lang)) ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                    else return "Error: English OCR not found.";
                }

                if (sourceLangHint == "Japanese")
                {
                    var lang = new Windows.Globalization.Language("ja-JP");
                    if (OcrEngine.IsLanguageSupported(lang)) ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                    else
                        return
                            "Error: Japanese OCR not installed in Windows. Go to Windows Settings -> Time & Language -> Language -> Add 'Japanese' and ensure 'Optical character recognition' is checked.";
                }
                else if (sourceLangHint == "Russian")
                {
                    var lang = new Windows.Globalization.Language("ru-RU");
                    if (OcrEngine.IsLanguageSupported(lang)) ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                    else
                        return
                            "Error: Russian OCR not installed in Windows. Go to Windows Settings -> Add a language -> 'Russian' with OCR.";
                }
                else if (sourceLangHint == "English")
                {
                    var lang = new Windows.Globalization.Language("en-US");
                    if (OcrEngine.IsLanguageSupported(lang)) ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                    else return "Error: English OCR not found.";
                }

                if (ocrEngine == null)
                {
                    return "Error: OCR Engine could not be initialized for the selected language.";
                }

                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                return ocrResult.Text;
            }
        }

        #endregion
    }
}