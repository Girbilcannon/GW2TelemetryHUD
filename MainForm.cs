using System;
using System.Drawing;
using System.Windows.Forms;

using Label = System.Windows.Forms.Label;

namespace GW2Telemetry
{
    public class MainForm : Form
    {
        private readonly TelemetryConfig _config;
        private readonly TelemetryWorker _worker;

        private TextBox _txtBroker = null!;
        private NumericUpDown _numPort = null!;
        private TextBox _txtTopic = null!;
        private NumericUpDown _numInterval = null!;
        private NumericUpDown _numColor = null!;

        private Button _btnSave = null!;
        private Button _btnStartStop = null!;
        private Label _lblStatus = null!;

        private bool _allowClose;

        public MainForm()
        {
            _config = ConfigManager.Load();
            _worker = new TelemetryWorker(_config, UpdateStatusFromWorker);

            Text = "GW2Telemetry";
            Width = 560;
            Height = 520;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();
            RefreshState("Starting telemetry...");

            _ = AutoStartTelemetry();
        }

        private void BuildUI()
        {
            int y = 15;

            Controls.Add(Header("MQTT Connection", ref y));

            Controls.Add(new Label
            {
                Text = "Broker:",
                Left = 20,
                Top = y + 4,
                AutoSize = true
            });

            _txtBroker = new TextBox
            {
                Left = 120,
                Top = y,
                Width = 390
            };
            Controls.Add(_txtBroker);

            y += 35;

            Controls.Add(new Label
            {
                Text = "Port:",
                Left = 20,
                Top = y + 4,
                AutoSize = true
            });

            _numPort = new NumericUpDown
            {
                Left = 120,
                Top = y,
                Width = 120,
                Minimum = 1,
                Maximum = 65535
            };
            Controls.Add(_numPort);

            y += 35;

            Controls.Add(new Label
            {
                Text = "Topic:",
                Left = 20,
                Top = y + 4,
                AutoSize = true
            });

            _txtTopic = new TextBox
            {
                Left = 120,
                Top = y,
                Width = 390
            };
            Controls.Add(_txtTopic);

            y += 45;

            Controls.Add(Header("Telemetry Settings", ref y));

            Controls.Add(new Label
            {
                Text = "Publish Interval (ms):",
                Left = 20,
                Top = y + 4,
                AutoSize = true
            });

            _numInterval = new NumericUpDown
            {
                Left = 180,
                Top = y,
                Width = 120,
                Minimum = 100,
                Maximum = 10000,
                Increment = 100
            };
            Controls.Add(_numInterval);

            y += 35;

            Controls.Add(new Label
            {
                Text = "Color:",
                Left = 20,
                Top = y + 4,
                AutoSize = true
            });

            _numColor = new NumericUpDown
            {
                Left = 180,
                Top = y,
                Width = 120,
                Minimum = 0,
                Maximum = int.MaxValue
            };
            Controls.Add(_numColor);

            y += 45;

            Controls.Add(Header("Status", ref y));

            _lblStatus = new Label
            {
                Left = 20,
                Top = y,
                Width = 490,
                Height = 50,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_lblStatus);

            y += 70;

            _btnSave = new Button
            {
                Text = "Save Settings",
                Left = 20,
                Top = y,
                Width = 150
            };
            _btnSave.Click += (_, _) => SaveSettings();
            Controls.Add(_btnSave);

            _btnStartStop = new Button
            {
                Text = "Start Telemetry",
                Left = 190,
                Top = y,
                Width = 150
            };
            _btnStartStop.Click += async (_, _) => await ToggleTelemetryAsync();
            Controls.Add(_btnStartStop);

            y += 50;

            AddTip("• Make sure Guild Wars 2 is running and fully loaded into a map.", ref y);
            AddTip("• Closing this window does not stop telemetry. Use the tray menu to fully exit.", ref y);
            AddTip("• Settings can only be changed while telemetry is stopped.", ref y);
        }

        private void UpdateStatusFromWorker(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => _lblStatus.Text = message));
                return;
            }

            _lblStatus.Text = message;
        }

        private void RefreshState(string? statusText = null)
        {
            _txtBroker.Text = _config.Broker;
            _numPort.Value = _config.Port;
            _txtTopic.Text = _config.Topic;
            _numInterval.Value = _config.PublishIntervalMs;
            _numColor.Value = _config.Color;

            bool isRunning = _worker.IsRunning;

            _txtBroker.Enabled = !isRunning;
            _numPort.Enabled = !isRunning;
            _txtTopic.Enabled = !isRunning;
            _numInterval.Enabled = !isRunning;
            _numColor.Enabled = !isRunning;
            _btnSave.Enabled = !isRunning;

            _btnStartStop.Text = isRunning ? "Stop Telemetry" : "Start Telemetry";

            if (!string.IsNullOrWhiteSpace(statusText))
                _lblStatus.Text = statusText;
        }

        private void SaveSettings()
        {
            if (_worker.IsRunning)
            {
                RefreshState("Stop telemetry before changing settings.");
                return;
            }

            _config.Broker = _txtBroker.Text.Trim();
            _config.Port = (int)_numPort.Value;
            _config.Topic = _txtTopic.Text.Trim();
            _config.PublishIntervalMs = (int)_numInterval.Value;
            _config.Color = (int)_numColor.Value;

            ConfigManager.Save(_config);
            RefreshState("Settings saved.");
        }

        private async System.Threading.Tasks.Task AutoStartTelemetry()
        {
            try
            {
                await _worker.StartAsync();
                RefreshState();
            }
            catch (Exception ex)
            {
                RefreshState($"Auto-start failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ToggleTelemetryAsync()
        {
            if (_worker.IsRunning)
            {
                await _worker.StopAsync();
                RefreshState();
            }
            else
            {
                SaveSettings();

                try
                {
                    await _worker.StartAsync();
                    RefreshState();
                }
                catch (Exception ex)
                {
                    RefreshState($"Failed to start telemetry: {ex.Message}");
                }
            }
        }

        public async System.Threading.Tasks.Task StopTelemetryForExitAsync()
        {
            if (_worker.IsRunning)
            {
                try
                {
                    await _worker.StopAsync();
                }
                catch
                {
                    // Ignore shutdown errors during exit
                }
            }
        }

        private static Label Header(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Left = 15,
                Top = y,
                AutoSize = true
            };

            y += 28;
            return lbl;
        }

        private void AddTip(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Left = 30,
                Top = y,
                AutoSize = true,
                MaximumSize = new Size(500, 0)
            };

            Controls.Add(lbl);
            y += lbl.PreferredHeight + 6;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        public void AllowExit()
        {
            _allowClose = true;
        }
    }
}