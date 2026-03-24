using System.Configuration;
using System.Data;
using System.Windows;
using System.Reflection;

namespace ScreenTranslator2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mainWindow = new MainWindow();
            
            // To ensure the global hotkey registers, the window handle must be created.
            var interopHelper = new System.Windows.Interop.WindowInteropHelper(_mainWindow);
            interopHelper.EnsureHandle();

            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
            
            // Use the application's icon
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Screen Translator";

            CreateContextMenu();
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            
            var showMenuItem = new System.Windows.Forms.ToolStripMenuItem("Show Main Window");
            showMenuItem.Click += (s, args) => ShowMainWindow();
            _notifyIcon.ContextMenuStrip.Items.Add(showMenuItem);
            
            _notifyIcon.ContextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            
            var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, args) => ExitApplication();
            _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        }

        private void ShowMainWindow()
        {
            if (_mainWindow.IsVisible)
            {
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                }
                _mainWindow.Activate();
            }
            else
            {
                _mainWindow.Show();
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                }
                _mainWindow.Activate();
            }
        }

        private void ExitApplication()
        {
            _notifyIcon.Dispose();
            _mainWindow.ForceClose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
