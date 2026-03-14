using System;
using System.Drawing;
using System.Windows.Forms;

namespace GW2Telemetry
{
    internal static class Program
    {
        private static NotifyIcon? _trayIcon;
        private static MainForm? _mainForm;
        private static Icon? _trayAppIcon;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var stream = typeof(Program).Assembly
                .GetManifestResourceStream("GW2Telemetry.Assets.tray.png");

            if (stream == null)
            {
                MessageBox.Show(
                    "Could not load tray icon resource: GW2Telemetry.Assets.tray.png",
                    "GW2Telemetry",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            using var trayBitmap = new Bitmap(stream);
            _trayAppIcon = Icon.FromHandle(trayBitmap.GetHicon());

            _trayIcon = new NotifyIcon
            {
                Icon = _trayAppIcon,
                Text = "GW2Telemetry",
                Visible = true,
                ContextMenuStrip = BuildTrayMenu()
            };

            _trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ShowMainWindow();
            };

            ShowMainWindow();
            Application.Run();
        }

        private static ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, async (_, _) => await ExitAppAsync());

            return menu;
        }

        private static void ShowMainWindow()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
                _mainForm = new MainForm();

            if (!_mainForm.Visible)
                _mainForm.Show();

            if (_mainForm.WindowState == FormWindowState.Minimized)
                _mainForm.WindowState = FormWindowState.Normal;

            _mainForm.BringToFront();
            _mainForm.Activate();
        }

        private static async System.Threading.Tasks.Task ExitAppAsync()
        {
            try
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    await _mainForm.StopTelemetryForExitAsync();
                    _mainForm.AllowExit();
                    _mainForm.Close();
                    _mainForm = null;
                }
            }
            finally
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }

                if (_trayAppIcon != null)
                {
                    _trayAppIcon.Dispose();
                    _trayAppIcon = null;
                }

                Application.Exit();
            }
        }
    }
}