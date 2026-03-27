using System;
using System.Drawing;
using System.Windows.Forms;

/*
    MainForm.cs

    Main application window for GW2Telemetry.

    Responsibilities:
    - Builds and manages the WinForms HUD-style interface
    - Loads current config values into the UI
    - Saves user settings back to config
    - Starts and stops telemetry through TelemetryWorker
    - Starts and stops the local status server
    - Displays current game, MumbleLink, and telemetry state
    - Shows live character, map, and position information
    - Updates effective MQTT topic preview and color preview
    - Provides tray-friendly hide/minimize behavior
*/

namespace GW2Telemetry
{
    public class MainForm : Form
    {
        private readonly TelemetryConfig _config;
        private readonly TelemetryWorker _worker;
        private readonly TelemetryLocalServer _localServer;
        private readonly ToolTip _toolTip;

        private Panel _titleBar = null!;
        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private StatusChip _chipTopStatus = null!;
        private HudButton _btnMinimize = null!;
        private HudButton _btnHide = null!;

        private HudCard _cardConnection = null!;
        private HudCard _cardTelemetry = null!;
        private HudCard _cardGame = null!;
        private HudCard _cardLive = null!;

        private ComboBox _cmbServerType = null!;
        private Label _lblHostLabel = null!;
        private TextBox _txtBroker = null!;
        private Label _lblPortLabel = null!;
        private NumericUpDown _numPort = null!;
        private Label _lblTopicLabel = null!;
        private TextBox _txtTopic = null!;
        private Label _lblEventCodeLabel = null!;
        private TextBox _txtEventCode = null!;
        private Label _lblEffectiveTopicLabel = null!;
        private Label _lblEffectiveTopic = null!;
        private Label _lblJsonRoot = null!;
        private Label _lblJsonStatus = null!;
        private Label _lblJsonMumble = null!;

        private NumericUpDown _numInterval = null!;
        private NumericUpDown _numColor = null!;
        private HudButton _btnPickColor = null!;
        private Panel _pnlColorPreview = null!;
        private Label _lblColorHex = null!;

        private Panel _pnlGameStatus = null!;
        private Label _lblGameStatus = null!;
        private Label _lblGameClientStatus = null!;
        private HudButton _btnRefreshMumble = null!;

        private Label _lblLiveCharacter = null!;
        private Label _lblLiveMap = null!;
        private Label _lblLivePosition = null!;
        private Label _lblStatus = null!;

        private HudButton _btnSave = null!;
        private HudButton _btnStartStop = null!;

        private bool _allowClose;
        private Point _dragStart;

        public MainForm()
        {
            _config = ConfigManager.Load();
            _worker = new TelemetryWorker(_config, UpdateStatusFromWorker);
            _localServer = new TelemetryLocalServer(_config.LocalServerPort);

            _toolTip = new ToolTip
            {
                AutomaticDelay = 250,
                AutoPopDelay = 12000,
                InitialDelay = 300,
                ReshowDelay = 100,
                ShowAlways = true
            };

            MumbleBridgeService.Start();
            _localServer.Start();

            Text = "GW2Telemetry";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 920;
            Height = 600;
            BackColor = Theme.AppBack;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            Opacity = 0.95;

            BuildUI();
            RefreshState("Starting telemetry...");
            _lblSubtitle.Text = "v1.2.0  •  Girbilcannon.8259";

            _ = AutoStartTelemetry();
            _ = StartStatusTimer();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyRoundedWindow();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (WindowState == FormWindowState.Normal)
                ApplyRoundedWindow();
        }

        private void ApplyRoundedWindow()
        {
            using var path = Theme.CreateRoundRect(new Rectangle(0, 0, Width, Height), 24);
            Region = new Region(path);
        }

        private void BuildUI()
        {
            SuspendLayout();

            BuildTitleBar();
            BuildCards();
            BuildFooterButtons();

            ResumeLayout(false);
        }

        private void BuildTitleBar()
        {
            _titleBar = new Panel
            {
                Left = 0,
                Top = 0,
                Width = Width,
                Height = 74,
                BackColor = Theme.TitleBar,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _titleBar.MouseDown += TitleBar_MouseDown;
            _titleBar.MouseMove += TitleBar_MouseMove;

            _lblTitle = new Label
            {
                Text = "GW2 Telemetry HUD",
                Left = 24,
                Top = 16,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold)
            };

            _lblSubtitle = new Label
            {
                Text = "v1.1.1",
                Left = 26,
                Top = 43,
                AutoSize = true,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular)
            };

            _chipTopStatus = new StatusChip
            {
                Left = 610,
                Top = 22,
                Width = 118,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ChipText = "Starting",
                ChipColor = Theme.Warning
            };

            _btnMinimize = new HudButton
            {
                Text = "—",
                Left = 796,
                Top = 20,
                Width = 38,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;

            _btnHide = new HudButton
            {
                Text = "×",
                Left = 840,
                Top = 20,
                Width = 38,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnHide.Click += (_, _) => Hide();

            _titleBar.Controls.Add(_lblTitle);
            _titleBar.Controls.Add(_lblSubtitle);
            _titleBar.Controls.Add(_chipTopStatus);
            _titleBar.Controls.Add(_btnMinimize);
            _titleBar.Controls.Add(_btnHide);

            Controls.Add(_titleBar);
        }

        private void BuildCards()
        {
            int left = 24;
            int top = 82;
            int gap = 18;

            _cardConnection = new HudCard
            {
                Left = left,
                Top = top,
                Width = 410,
                Height = 275,
                Title = "Connection"
            };
            BuildConnectionCard();
            Controls.Add(_cardConnection);

            _cardTelemetry = new HudCard
            {
                Left = left + 410 + gap,
                Top = top,
                Width = 444,
                Height = 235,
                Title = "Telemetry Settings"
            };
            BuildTelemetryCard();
            Controls.Add(_cardTelemetry);

            _cardGame = new HudCard
            {
                Left = left,
                Top = top + 275 + gap,
                Width = 410,
                Height = 145,
                Title = "Game Connection"
            };
            BuildGameCard();
            Controls.Add(_cardGame);

            _cardLive = new HudCard
            {
                Left = left + 410 + gap,
                Top = top + 235 + gap,
                Width = 444,
                Height = 245,
                Title = "Live Status"
            };
            BuildLiveCard();
            Controls.Add(_cardLive);
        }

        private void BuildConnectionCard()
        {
            int xLabel = 18;
            int xInput = 135;
            int rowY = 22;

            _cardConnection.Controls.Add(Theme.MakeLabel("Server Type", xLabel, rowY + 5, true, true));

            _cmbServerType = new ComboBox
            {
                Left = xInput,
                Top = rowY,
                Width = 170,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbServerType.Items.AddRange(new object[]
            {
                TelemetryConfig.ServerTypeUdp,
                TelemetryConfig.ServerTypeMqtt,
                TelemetryConfig.ServerTypeJsonOnly
            });
            _cmbServerType.FlatStyle = FlatStyle.Flat;
            _cmbServerType.BackColor = Theme.InputBack;
            _cmbServerType.ForeColor = Theme.Text;
            _cmbServerType.SelectedIndexChanged += (_, _) =>
            {
                ApplyConnectionModeUi();
                UpdateConnectionPreview();
            };
            _cardConnection.Controls.Add(_cmbServerType);

            rowY += 40;
            _lblHostLabel = Theme.MakeLabel("Server", xLabel, rowY + 5, true, true);
            _cardConnection.Controls.Add(_lblHostLabel);

            _txtBroker = new TextBox
            {
                Left = xInput,
                Top = rowY,
                Width = 238
            };
            Theme.ApplyTextBoxStyle(_txtBroker);
            _txtBroker.TextChanged += (_, _) => UpdateConnectionPreview();
            _cardConnection.Controls.Add(_txtBroker);

            rowY += 40;
            _lblPortLabel = Theme.MakeLabel("Port", xLabel, rowY + 5, true, true);
            _cardConnection.Controls.Add(_lblPortLabel);

            _numPort = new NumericUpDown
            {
                Left = xInput,
                Top = rowY,
                Width = 120,
                Minimum = 1,
                Maximum = 65535
            };
            Theme.ApplyNumericStyle(_numPort);
            _numPort.ValueChanged += (_, _) => UpdateConnectionPreview();
            _cardConnection.Controls.Add(_numPort);

            rowY += 40;
            _lblTopicLabel = Theme.MakeLabel("Base Topic", xLabel, rowY + 5, true, true);
            _cardConnection.Controls.Add(_lblTopicLabel);

            _txtTopic = new TextBox
            {
                Left = xInput,
                Top = rowY,
                Width = 238
            };
            Theme.ApplyTextBoxStyle(_txtTopic);
            _txtTopic.TextChanged += (_, _) => UpdateConnectionPreview();
            _cardConnection.Controls.Add(_txtTopic);

            rowY += 40;
            _lblEventCodeLabel = Theme.MakeLabel("Event Code", xLabel, rowY + 5, true, true);
            _cardConnection.Controls.Add(_lblEventCodeLabel);

            _txtEventCode = new TextBox
            {
                Left = xInput,
                Top = rowY,
                Width = 140
            };
            Theme.ApplyTextBoxStyle(_txtEventCode);
            _txtEventCode.TextChanged += (_, _) => UpdateConnectionPreview();
            _cardConnection.Controls.Add(_txtEventCode);

            rowY += 44;
            _lblEffectiveTopicLabel = Theme.MakeLabel("Preview", xLabel, rowY + 4, true, true);
            _cardConnection.Controls.Add(_lblEffectiveTopicLabel);

            _lblEffectiveTopic = Theme.MakeLabel("-", xInput, rowY + 4, false, false, 238);
            _lblEffectiveTopic.ForeColor = Theme.Accent;
            _lblEffectiveTopic.Font = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold);
            _cardConnection.Controls.Add(_lblEffectiveTopic);

            _lblJsonRoot = Theme.MakeLabel("-", xLabel, 104, false, false, 350);
            _lblJsonRoot.ForeColor = Theme.Accent;
            _lblJsonRoot.Cursor = Cursors.Hand;
            _lblJsonRoot.Click += (_, _) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _lblJsonRoot.Text.Replace("Root: ", ""),
                    UseShellExecute = true
                });
            };
            _cardConnection.Controls.Add(_lblJsonRoot);

            _lblJsonStatus = Theme.MakeLabel("-", xLabel, 138, false, false, 350);
            _lblJsonStatus.ForeColor = Theme.Accent;
            _lblJsonStatus.Cursor = Cursors.Hand;
            _lblJsonStatus.Click += (_, _) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _lblJsonStatus.Text.Replace("Status: ", ""),
                    UseShellExecute = true
                });
            };
            _cardConnection.Controls.Add(_lblJsonStatus);

            _lblJsonMumble = Theme.MakeLabel("-", xLabel, 172, false, false, 350);
            _lblJsonMumble.ForeColor = Theme.Accent;
            _lblJsonMumble.Cursor = Cursors.Hand;
            _lblJsonMumble.Click += (_, _) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _lblJsonMumble.Text.Replace("Mumble: ", ""),
                    UseShellExecute = true
                });
            };
            _cardConnection.Controls.Add(_lblJsonMumble);
        }

        private void BuildTelemetryCard()
        {
            int xLabel = 18;
            int xInput = 145;
            int rowY = 42;

            _cardTelemetry.Controls.Add(Theme.MakeLabel("Publish Interval", xLabel, rowY + 5, true, true));

            _numInterval = new NumericUpDown
            {
                Left = xInput,
                Top = rowY,
                Width = 110,
                Minimum = 200,
                Maximum = 10000,
                Increment = 100
            };
            Theme.ApplyNumericStyle(_numInterval);
            _cardTelemetry.Controls.Add(_numInterval);

            _cardTelemetry.Controls.Add(Theme.MakeLabel("ms", xInput + 118, rowY + 5, true));

            rowY += 48;
            _cardTelemetry.Controls.Add(Theme.MakeLabel("Color Value", xLabel, rowY + 5, true, true));

            _numColor = new NumericUpDown
            {
                Left = xInput,
                Top = rowY,
                Width = 120,
                Minimum = 0,
                Maximum = 16777215
            };
            Theme.ApplyNumericStyle(_numColor);
            _numColor.ValueChanged += (_, _) =>
            {
                UpdateColorPreview();
                UpdateStatusDisplay();
            };
            _cardTelemetry.Controls.Add(_numColor);

            _btnPickColor = new HudButton
            {
                Text = "Pick Color",
                Left = xInput + 136,
                Top = rowY - 1,
                Width = 110,
                Height = 32
            };
            _btnPickColor.Click += (_, _) => PickColor();
            _cardTelemetry.Controls.Add(_btnPickColor);

            rowY += 48;
            _cardTelemetry.Controls.Add(Theme.MakeLabel("Color Hex", xLabel, rowY + 5, true, true));

            _pnlColorPreview = new Panel
            {
                Left = xInput,
                Top = rowY + 1,
                Width = 34,
                Height = 22,
                BackColor = Color.Black
            };
            _pnlColorPreview.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, _pnlColorPreview.Width - 1, _pnlColorPreview.Height - 1);
            };
            _cardTelemetry.Controls.Add(_pnlColorPreview);

            _lblColorHex = Theme.MakeLabel("#000000", xInput + 46, rowY + 4, false, true, 120);
            _lblColorHex.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            _lblColorHex.Cursor = Cursors.Hand;
            _lblColorHex.Click += (_, _) => Clipboard.SetText(_lblColorHex.Text);
            _cardTelemetry.Controls.Add(_lblColorHex);

            rowY += 48;
            var helper = Theme.MakeLabel(
                "Click the hex value to copy. This is the color of your GPS Marker",
                xLabel,
                rowY,
                true,
                false,
                380);

            helper.MaximumSize = new Size(380, 40);
            _cardTelemetry.Controls.Add(helper);
        }

        private void BuildGameCard()
        {
            _pnlGameStatus = new Panel
            {
                Left = 18,
                Top = 50,
                Width = 22,
                Height = 22,
                BackColor = Theme.Danger
            };
            _pnlGameStatus.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(_pnlGameStatus.BackColor);
                using var pen = new Pen(Color.FromArgb(120, _pnlGameStatus.BackColor));
                e.Graphics.FillEllipse(brush, 1, 1, 18, 18);
                e.Graphics.DrawEllipse(pen, 1, 1, 18, 18);
            };

            _lblGameStatus = Theme.MakeLabel("Waiting for Guild Wars 2 / MumbleLink...", 50, 48, false, true, 300);
            _lblGameStatus.Font = new Font("Segoe UI Semibold", 10.25f, FontStyle.Bold);

            _lblGameClientStatus = Theme.MakeLabel("Game Client: checking...", 50, 74, true, false, 340);
            _lblGameClientStatus.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _lblGameClientStatus.AutoEllipsis = true;
            _lblGameClientStatus.Cursor = Cursors.Help;

            _btnRefreshMumble = new HudButton
            {
                Text = "Refresh MumbleLink",
                Left = 18,
                Top = 95,
                Width = 180,
                Height = 36
            };
            _btnRefreshMumble.Click += (_, _) => RefreshMumbleLink();

            _cardGame.Controls.Add(_pnlGameStatus);
            _cardGame.Controls.Add(_lblGameStatus);
            _cardGame.Controls.Add(_lblGameClientStatus);
            _cardGame.Controls.Add(_btnRefreshMumble);
        }

        private void BuildLiveCard()
        {
            int labelX = 18;
            int valueX = 126;
            int rowY = 44;

            _cardLive.Controls.Add(Theme.MakeLabel("Character", labelX, rowY, true, true));
            _lblLiveCharacter = Theme.MakeLabel("-", valueX, rowY, false, true, 280);
            _cardLive.Controls.Add(_lblLiveCharacter);

            rowY += 34;
            _cardLive.Controls.Add(Theme.MakeLabel("Map ID", labelX, rowY, true, true));
            _lblLiveMap = Theme.MakeLabel("-", valueX, rowY, false, true, 280);
            _cardLive.Controls.Add(_lblLiveMap);

            rowY += 34;
            _cardLive.Controls.Add(Theme.MakeLabel("Position", labelX, rowY, true, true));
            _lblLivePosition = Theme.MakeLabel("-", valueX, rowY, false, true, 290);
            _cardLive.Controls.Add(_lblLivePosition);

            rowY += 40;
            _cardLive.Controls.Add(Theme.MakeLabel("Worker Status", labelX, rowY, true, true));

            _lblStatus = new Label
            {
                Left = 18,
                Top = rowY + 22,
                Width = 390,
                Height = 54,
                ForeColor = Theme.Text,
                BackColor = Theme.CardBackAlt,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.1f, FontStyle.Regular),
                Padding = new Padding(10)
            };
            _cardLive.Controls.Add(_lblStatus);
        }

        private void BuildFooterButtons()
        {
            _btnSave = new HudButton
            {
                Text = "Save Settings",
                Left = 24,
                Top = 535,
                Width = 150,
                Height = 42
            };
            _btnSave.Click += (_, _) => SaveSettings();

            _btnStartStop = new HudButton
            {
                Text = "Start Telemetry",
                Left = 186,
                Top = 535,
                Width = 170,
                Height = 42,
                AccentStyle = true
            };
            _btnStartStop.Click += async (_, _) => await ToggleTelemetryAsync();

            Controls.Add(_btnSave);
            Controls.Add(_btnStartStop);
        }

        private void UpdateStatusFromWorker(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    _lblStatus.Text = message;
                    UpdateStatusDisplay();
                }));
                return;
            }

            _lblStatus.Text = message;
            UpdateStatusDisplay();
        }

        private void RefreshState(string? statusText = null)
        {
            _cmbServerType.SelectedItem = _config.NormalizedServerType;
            if (_cmbServerType.SelectedItem == null)
                _cmbServerType.SelectedItem = TelemetryConfig.ServerTypeUdp;

            _txtBroker.Text = _config.IsMqttSelected
                ? _config.MqttBroker
                : _config.UdpHost;

            _numPort.Value = _config.IsMqttSelected
                ? Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, _config.MqttPort))
                : Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, _config.UdpPort));

            _txtTopic.Text = _config.MqttTopic;
            _txtEventCode.Text = _config.EventCode ?? string.Empty;
            _numInterval.Value = Math.Max(_numInterval.Minimum, Math.Min(_numInterval.Maximum, _config.PublishIntervalMs));
            _numColor.Value = Math.Max(_numColor.Minimum, Math.Min(_numColor.Maximum, _config.Color));

            bool isRunning = _worker.IsRunning;

            _cmbServerType.Enabled = !isRunning;
            _txtBroker.Enabled = !isRunning;
            _numPort.Enabled = !isRunning;
            _txtTopic.Enabled = !isRunning;
            _txtEventCode.Enabled = !isRunning;
            _numInterval.Enabled = !isRunning;
            _numColor.Enabled = !isRunning;
            _btnPickColor.Enabled = !isRunning;
            _btnSave.Enabled = !isRunning;

            _btnStartStop.Text = isRunning ? "Stop Telemetry" : "Start Telemetry";

            if (!string.IsNullOrWhiteSpace(statusText))
                _lblStatus.Text = statusText;

            ApplyConnectionModeUi();
            UpdateConnectionPreview();
            UpdateColorPreview();
            UpdateStatusDisplay();
        }

        private void SaveSettings()
        {
            if (_worker.IsRunning)
            {
                RefreshState("Stop telemetry before changing settings.");
                return;
            }

            string selectedType = (_cmbServerType.SelectedItem?.ToString() ?? TelemetryConfig.ServerTypeUdp).Trim();
            _config.ServerType = selectedType;
            _config.EventCode = _txtEventCode.Text.Trim();
            _config.PublishIntervalMs = Math.Max(200, (int)_numInterval.Value);
            _config.Color = (int)_numColor.Value;

            if (string.Equals(selectedType, TelemetryConfig.ServerTypeMqtt, StringComparison.OrdinalIgnoreCase))
            {
                _config.MqttBroker = _txtBroker.Text.Trim();
                _config.MqttPort = (int)_numPort.Value;
                _config.MqttTopic = _txtTopic.Text.Trim();
            }
            else if (string.Equals(selectedType, TelemetryConfig.ServerTypeUdp, StringComparison.OrdinalIgnoreCase))
            {
                _config.UdpHost = _txtBroker.Text.Trim();
                _config.UdpPort = (int)_numPort.Value;
            }

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
                return;
            }

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

        private async void RefreshMumbleLink()
        {
            _lblStatus.Text = "Refreshing MumbleLink bridge...";
            UpdateStatusDisplay();

            bool connected = await TelemetryWorker.ProbeMumbleBurstAsync();

            _lblStatus.Text = connected
                ? "MumbleLink detected."
                : "MumbleLink not detected after burst retry.";

            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            var bridge = MumbleBridgeService.GetState();
            bool gameRunning = bridge.IsGameRunning;
            bool connected = bridge.IsConnected;
            var snapshot = bridge.Snapshot;
            bool telemetryReady = connected && snapshot != null && snapshot.IsUsableForTelemetry;

            _pnlGameStatus.BackColor = telemetryReady
                ? Theme.Success
                : (connected ? Theme.Warning : Theme.Danger);
            _pnlGameStatus.Invalidate();

            if (!gameRunning)
            {
                _lblGameStatus.Text = "Guild Wars 2 not running";
                _lblGameClientStatus.Text = "Game Client: not detected";
                _lblGameClientStatus.ForeColor = Theme.MutedText;
            }
            else if (!connected)
            {
                _lblGameStatus.Text = "Game running, waiting for MumbleLink...";
                _lblGameClientStatus.Text = string.IsNullOrWhiteSpace(bridge.FailureReason)
                    ? "Game Client: detected"
                    : $"Game Client: detected ({bridge.FailureReason})";
                _lblGameClientStatus.ForeColor = Theme.Warning;
            }
            else if (!telemetryReady)
            {
                _lblGameStatus.Text = "MumbleLink detected, waiting for telemetry...";
                _lblGameClientStatus.Text = string.IsNullOrWhiteSpace(bridge.FailureReason)
                    ? "Game Client: detected"
                    : $"Game Client: detected ({bridge.FailureReason})";
                _lblGameClientStatus.ForeColor = Theme.Warning;
            }
            else
            {
                _lblGameStatus.Text = "Guild Wars 2 / MumbleLink detected";
                _lblGameClientStatus.Text = "Game Client: detected";
                _lblGameClientStatus.ForeColor = Theme.Success;
            }

            _toolTip.SetToolTip(_lblGameClientStatus, _lblGameClientStatus.Text);

            if (telemetryReady)
            {
                _lblLiveCharacter.Text = string.IsNullOrWhiteSpace(_worker.LastCharacterName) ? "-" : _worker.LastCharacterName;
                _lblLiveMap.Text = _worker.LastMapId > 0 ? _worker.LastMapId.ToString() : "-";
                _lblLivePosition.Text = string.IsNullOrWhiteSpace(_worker.LastPositionText) ? "-" : _worker.LastPositionText;
            }
            else
            {
                _lblLiveCharacter.Text = "-";
                _lblLiveMap.Text = "-";
                _lblLivePosition.Text = "-";
            }

            if (_worker.IsRunning && telemetryReady)
            {
                _chipTopStatus.ChipText = "Live";
                _chipTopStatus.ChipColor = Theme.Success;
            }
            else if (_worker.IsRunning && connected)
            {
                _chipTopStatus.ChipText = "Link";
                _chipTopStatus.ChipColor = Theme.Warning;
            }
            else if (_worker.IsRunning && gameRunning)
            {
                _chipTopStatus.ChipText = "Waiting";
                _chipTopStatus.ChipColor = Theme.Warning;
            }
            else if (_worker.IsRunning)
            {
                _chipTopStatus.ChipText = "No Game";
                _chipTopStatus.ChipColor = Theme.Danger;
            }
            else
            {
                _chipTopStatus.ChipText = "Stopped";
                _chipTopStatus.ChipColor = Theme.Danger;
            }
        }

        private static string BuildEffectiveTopic(string baseTopic, string? eventCode)
        {
            string normalizedBase = (baseTopic ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedBase))
                return "/";

            normalizedBase = normalizedBase.Trim('/');

            if (string.IsNullOrWhiteSpace(normalizedBase))
                return "/";

            string normalizedEvent = (eventCode ?? string.Empty).Trim().Trim('/');

            return string.IsNullOrWhiteSpace(normalizedEvent)
                ? "/" + normalizedBase
                : "/" + normalizedBase + "/" + normalizedEvent;
        }

        private void ApplyConnectionModeUi()
        {
            string selectedType = (_cmbServerType.SelectedItem?.ToString() ?? TelemetryConfig.ServerTypeUdp).Trim();

            bool isMqtt = string.Equals(selectedType, TelemetryConfig.ServerTypeMqtt, StringComparison.OrdinalIgnoreCase);
            bool isUdp = string.Equals(selectedType, TelemetryConfig.ServerTypeUdp, StringComparison.OrdinalIgnoreCase);
            bool isJsonOnly = string.Equals(selectedType, TelemetryConfig.ServerTypeJsonOnly, StringComparison.OrdinalIgnoreCase);

            _cardConnection.Title = isMqtt
                ? "MQTT Connection"
                : (isUdp ? "UDP Connection" : "Local JSON Output");

            _lblHostLabel.Visible = !isJsonOnly;
            _txtBroker.Visible = !isJsonOnly;
            _lblPortLabel.Visible = !isJsonOnly;
            _numPort.Visible = !isJsonOnly;
            _lblTopicLabel.Visible = isMqtt;
            _txtTopic.Visible = isMqtt;
            _lblEventCodeLabel.Visible = !isJsonOnly;
            _txtEventCode.Visible = !isJsonOnly;
            _lblEffectiveTopicLabel.Visible = !isJsonOnly;
            _lblEffectiveTopic.Visible = !isJsonOnly;

            _lblJsonRoot.Visible = isJsonOnly;
            _lblJsonStatus.Visible = isJsonOnly;
            _lblJsonMumble.Visible = isJsonOnly;

            _lblHostLabel.Text = isMqtt ? "Broker" : "Server";
            _lblPortLabel.Text = "Port";
            _lblTopicLabel.Text = "Base Topic";
            _lblEventCodeLabel.Text = "Event Code";
            _lblEffectiveTopicLabel.Text = isMqtt ? "Effective Topic" : "Packet Target";

            if (isMqtt)
            {
                if (_txtBroker.Text != _config.MqttBroker)
                    _txtBroker.Text = _config.MqttBroker;
                _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, _config.MqttPort));
            }
            else if (isUdp)
            {
                if (_txtBroker.Text != _config.UdpHost)
                    _txtBroker.Text = _config.UdpHost;
                _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, _config.UdpPort));
            }

            string root = $"http://localhost:{_config.LocalServerPort}";
            _lblJsonRoot.Text = $"Root: {root}/";
            _lblJsonStatus.Text = $"Status: {root}/status";
            _lblJsonMumble.Text = $"Mumble: {root}/mumble";
        }

        private void UpdateConnectionPreview()
        {
            string selectedType = (_cmbServerType.SelectedItem?.ToString() ?? TelemetryConfig.ServerTypeUdp).Trim();

            if (string.Equals(selectedType, TelemetryConfig.ServerTypeMqtt, StringComparison.OrdinalIgnoreCase))
            {
                string effective = BuildEffectiveTopic(_txtTopic.Text, _txtEventCode.Text);
                _lblEffectiveTopic.Text = effective == "/" ? "-" : effective;
                return;
            }

            if (string.Equals(selectedType, TelemetryConfig.ServerTypeUdp, StringComparison.OrdinalIgnoreCase))
            {
                string host = _txtBroker.Text.Trim();
                string eventCode = _txtEventCode.Text.Trim();

                if (string.IsNullOrWhiteSpace(host))
                {
                    _lblEffectiveTopic.Text = "-";
                    return;
                }

                _lblEffectiveTopic.Text = string.IsNullOrWhiteSpace(eventCode)
                    ? $"{host}:{(int)_numPort.Value}"
                    : $"{host}:{(int)_numPort.Value}  •  sessionCode={eventCode}";
                return;
            }

            _lblEffectiveTopic.Text = "-";
        }

        private void PickColor()
        {
            using var dialog = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                SolidColorOnly = false,
                Color = IntToColor((int)_numColor.Value)
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                _numColor.Value = ColorToInt(dialog.Color);
        }

        private void UpdateColorPreview()
        {
            int value = (int)_numColor.Value;
            var color = IntToColor(value);

            _pnlColorPreview.BackColor = color;
            _pnlColorPreview.Invalidate();
            _lblColorHex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static Color IntToColor(int colorValue)
        {
            int r = (colorValue >> 16) & 0xFF;
            int g = (colorValue >> 8) & 0xFF;
            int b = colorValue & 0xFF;
            return Color.FromArgb(r, g, b);
        }

        private static int ColorToInt(Color color)
        {
            return (color.R << 16) | (color.G << 8) | color.B;
        }

        private async System.Threading.Tasks.Task StartStatusTimer()
        {
            while (!IsDisposed)
            {
                try
                {
                    if (IsHandleCreated)
                        BeginInvoke(new Action(UpdateStatusDisplay));
                }
                catch
                {
                }

                await System.Threading.Tasks.Task.Delay(500);
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
                }
            }

            try
            {
                await MumbleBridgeService.StopAsync();
            }
            catch
            {
            }

            try
            {
                await _localServer.StopAsync();
            }
            catch
            {
            }
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _dragStart = new Point(e.X, e.Y);
        }

        private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            Left += e.X - _dragStart.X;
            Top += e.Y - _dragStart.Y;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using var pen = new Pen(Color.FromArgb(70, Theme.Border));
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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