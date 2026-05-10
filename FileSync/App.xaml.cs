using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace FileSync
{
    public partial class App : System.Windows.Application
    {
        private static readonly string MutexName = "Global\\FileSync_SingleInstance_Mutex";
        private static Mutex? _mutex;
        private NotifyIcon? _trayIcon;
        private Icon? _appIcon;
        private bool _isExiting;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                ActivateExistingInstance();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            _appIcon = LoadAppIcon();
            
            // 给主窗口也设置图标
            if (MainWindow != null && _appIcon != null)
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        _appIcon.Save(ms);
                        ms.Position = 0;
                        var bitmapImg = new BitmapImage();
                        bitmapImg.BeginInit();
                        bitmapImg.StreamSource = ms;
                        bitmapImg.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImg.EndInit();
                        MainWindow.Icon = bitmapImg;
                    }
                }
                catch { }
            }

            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon,
                Visible = true,
                Text = "FileSync - 文件同步备份工具"
            };

            _trayIcon.ContextMenuStrip = new ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("显示主窗口", null, (s, _) => ShowMainWindow()));
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("退出", null, (s, _) => ForceExit()));

            _trayIcon.DoubleClick += (s, _) => ShowMainWindow();

            _trayIcon.ShowBalloonTip(3000, "FileSync", "FileSync 正在后台运行", ToolTipIcon.Info);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _trayIcon?.Dispose();
            _appIcon?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }

        private static void ActivateExistingInstance()
        {
            var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    if (IsIconic(process.MainWindowHandle))
                        ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
        }

        public void ShowMainWindow()
        {
            if (MainWindow == null) return;
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        public static void ForceExit()
        {
            if (Current is App app)
            {
                app._isExiting = true;
                ((MainWindow)app.MainWindow).ForceClose();
                app.Shutdown();
            }
        }

        public void HideMainWindow()
        {
            if (MainWindow != null)
            {
                MainWindow.Hide();
                _trayIcon?.ShowBalloonTip(2000, "FileSync", "已最小化到托盘，定时任务继续运行", ToolTipIcon.Info);
            }
        }

        public bool IsExiting => _isExiting;

        private static Icon LoadAppIcon()
        {
            // 优先从源项目目录加载（开发环境）
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string devPath = Path.Combine(baseDir, "..\\..\\file_sync_icon.ico");
                devPath = Path.GetFullPath(devPath);
                if (File.Exists(devPath))
                {
                    return new Icon(devPath);
                }
            }
            catch { }

            // 再尝试从应用程序基目录加载
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file_sync_icon.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }

            // 最后备用：创建简单图标
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(25, 118, 210));
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}
