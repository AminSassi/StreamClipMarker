using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StreamClipMarker.Core;

namespace StreamClipMarker
{
    public class MainForm : Form
    {
        private AppConfig _config;
        private SessionTimer _timer;
        private HotkeyManager _hotkeyManager;
        private ToastFeedback _toast;
        private RecordingDetector _recordingDetector;

        private SessionData _currentSession;
        private Timer _clockTimer;
        private Timer _countdownTimer;
        private int _countdownRemaining;

        private double _lastMarkerRawSeconds = -999;
        private string _lastMarkerFormatted = "--:--:--";
        private string _lastExportedTxtPath = "";
        private string _lastExportedFolder = "";
        private bool _isExiting = false;
        private bool _hasShownTrayBalloon = false;

        // UI Controls
        private PictureBox _picLogo;
        private Label _lblTitle;
        private Button _btnSettings;
        private Label _lblClock;
        private Label _lblStatus;
        private Label _lblRecordingFile;
        private Button _btnMarkClip;
        private Label _lblClipsCount;
        private ListView _lstMarkers;
        private Button _btnSessionAction;
        private Label _lblOffset;
        private Button _btnOffsetMinus;
        private Button _btnOffsetPlus;
        private Label _lblOffsetVal;
        private Button _btnOpenFile;
        private Button _btnOpenFolder;
        private ContextMenuStrip _markerContextMenu;

        // System Tray
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayContextMenu;
        private ToolStripMenuItem _trayMenuAction;

        private enum SessionState
        {
            Idle,
            WaitingForRecording,
            Countdown,
            Active,
            Finalizing,
            Ended
        }
        private SessionState _state = SessionState.Idle;

        public MainForm()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeComponent()
        {
            Text = "StreamClipMarker";
            Size = new Size(410, 640);
            MinimumSize = new Size(390, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 21, 26);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            DoubleBuffered = true;

            try
            {
                Icon = AppBranding.GetAppIcon();
            }
            catch { }

            // Header panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(26, 28, 35)
            };

            // Mini logo badge
            _picLogo = new PictureBox
            {
                Location = new Point(14, 10),
                Size = new Size(28, 28),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            try
            {
                _picLogo.Image = AppBranding.GetAppIcon().ToBitmap();
            }
            catch { }
            pnlHeader.Controls.Add(_picLogo);

            _lblTitle = new Label
            {
                Text = "STREAM CLIP MARKER",
                Location = new Point(48, 13),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 225, 235)
            };
            pnlHeader.Controls.Add(_lblTitle);

            _btnSettings = new Button
            {
                Text = "⚙ Settings",
                Location = new Point(302, 10),
                Size = new Size(84, 28),
                BackColor = Color.FromArgb(42, 45, 56),
                ForeColor = Color.FromArgb(200, 210, 225),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnSettings.FlatAppearance.BorderSize = 0;
            _btnSettings.Click += BtnSettings_Click;
            pnlHeader.Controls.Add(_btnSettings);
            Controls.Add(pnlHeader);

            // Center Container
            Panel pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 8, 16, 8),
                BackColor = Color.FromArgb(20, 21, 26)
            };
            Controls.Add(pnlMain);

            int curY = 4;

            // Stopwatch Clock
            _lblClock = new Label
            {
                Text = "00:00:00",
                Location = new Point(0, curY),
                Size = new Size(378, 48),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 32f, FontStyle.Bold),
                ForeColor = Color.FromArgb(240, 245, 255),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlMain.Controls.Add(_lblClock);
            curY += 50;

            // Status Indicator (State Machine Display)
            _lblStatus = new Label
            {
                Text = "○ IDLE",
                Location = new Point(0, curY),
                Size = new Size(378, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 140, 155),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlMain.Controls.Add(_lblStatus);
            curY += 22;

            // Detected Filename Label
            _lblRecordingFile = new Label
            {
                Text = "",
                Location = new Point(0, curY),
                Size = new Size(378, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
                ForeColor = Color.FromArgb(140, 180, 210),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlMain.Controls.Add(_lblRecordingFile);
            curY += 22;

            // Mark Clip Button
            _btnMarkClip = new Button
            {
                Text = "MARK CLIP — F8",
                Location = new Point(14, curY),
                Size = new Size(350, 46),
                BackColor = Color.FromArgb(0, 168, 107),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _btnMarkClip.FlatAppearance.BorderSize = 0;
            _btnMarkClip.Click += (s, e) => TriggerClipMark(true, false);
            pnlMain.Controls.Add(_btnMarkClip);
            curY += 52;

            // Clips Count & Offset bar
            Panel pnlStats = new Panel
            {
                Location = new Point(14, curY),
                Size = new Size(350, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _lblClipsCount = new Label
            {
                Text = "CLIPS MARKED: 0",
                Location = new Point(0, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 190, 205)
            };
            pnlStats.Controls.Add(_lblClipsCount);

            _lblOffset = new Label
            {
                Text = "Offset:",
                Location = new Point(200, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            pnlStats.Controls.Add(_lblOffset);

            _btnOffsetMinus = new Button
            {
                Text = "-",
                Location = new Point(246, 1),
                Size = new Size(22, 22),
                BackColor = Color.FromArgb(36, 38, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnOffsetMinus.FlatAppearance.BorderSize = 0;
            _btnOffsetMinus.Click += (s, e) => AdjustOffset(-1);
            pnlStats.Controls.Add(_btnOffsetMinus);

            _lblOffsetVal = new Label
            {
                Text = "+0s",
                Location = new Point(270, 4),
                Size = new Size(38, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            pnlStats.Controls.Add(_lblOffsetVal);

            _btnOffsetPlus = new Button
            {
                Text = "+",
                Location = new Point(310, 1),
                Size = new Size(22, 22),
                BackColor = Color.FromArgb(36, 38, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnOffsetPlus.FlatAppearance.BorderSize = 0;
            _btnOffsetPlus.Click += (s, e) => AdjustOffset(+1);
            pnlStats.Controls.Add(_btnOffsetPlus);

            pnlMain.Controls.Add(pnlStats);
            curY += 30;

            // Markers List View
            _lstMarkers = new ListView
            {
                Location = new Point(14, curY),
                Size = new Size(350, 195),
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BackColor = Color.FromArgb(28, 30, 38),
                ForeColor = Color.FromArgb(235, 240, 250),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _lstMarkers.Columns.Add("#", 38);
            _lstMarkers.Columns.Add("Marker", 78);
            _lstMarkers.Columns.Add("Clip Range", 130);
            _lstMarkers.Columns.Add("Note / Label", 90);
            _lstMarkers.DoubleClick += (s, e) => MenuEdit_Click(s, e);
            pnlMain.Controls.Add(_lstMarkers);

            // Context Menu for Markers
            _markerContextMenu = new ContextMenuStrip();
            ToolStripMenuItem menuEdit = new ToolStripMenuItem("Edit Note / Label");
            menuEdit.Click += MenuEdit_Click;
            ToolStripMenuItem menuDelete = new ToolStripMenuItem("Delete Selected Marker");
            menuDelete.Click += MenuDelete_Click;
            ToolStripMenuItem menuAddManual = new ToolStripMenuItem("Add Manual Timestamp...");
            menuAddManual.Click += MenuAddManual_Click;
            ToolStripMenuItem menuClear = new ToolStripMenuItem("Clear All Markers");
            menuClear.Click += MenuClear_Click;

            _markerContextMenu.Items.Add(menuEdit);
            _markerContextMenu.Items.Add(menuDelete);
            _markerContextMenu.Items.Add(new ToolStripSeparator());
            _markerContextMenu.Items.Add(menuAddManual);
            _markerContextMenu.Items.Add(menuClear);
            _lstMarkers.ContextMenuStrip = _markerContextMenu;

            // Bottom Panel for Actions
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                BackColor = Color.FromArgb(24, 25, 32),
                Padding = new Padding(14, 8, 14, 8)
            };
            Controls.Add(pnlBottom);

            _btnSessionAction = new Button
            {
                Text = "START SESSION",
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.FromArgb(0, 130, 230),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSessionAction.FlatAppearance.BorderSize = 0;
            _btnSessionAction.Click += BtnSessionAction_Click;
            pnlBottom.Controls.Add(_btnSessionAction);

            // Export Actions (shown when session ends)
            Panel pnlExportBtns = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Visible = false
            };

            _btnOpenFile = new Button
            {
                Text = "Open Timestamp File",
                Location = new Point(0, 2),
                Size = new Size(168, 26),
                BackColor = Color.FromArgb(42, 45, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25f)
            };
            _btnOpenFile.FlatAppearance.BorderSize = 0;
            _btnOpenFile.Click += (s, e) =>
            {
                if (File.Exists(_lastExportedTxtPath))
                {
                    Process.Start(_lastExportedTxtPath);
                }
            };
            pnlExportBtns.Controls.Add(_btnOpenFile);

            _btnOpenFolder = new Button
            {
                Text = "Open Output Folder",
                Location = new Point(178, 2),
                Size = new Size(168, 26),
                BackColor = Color.FromArgb(42, 45, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnOpenFolder.FlatAppearance.BorderSize = 0;
            _btnOpenFolder.Click += (s, e) =>
            {
                if (Directory.Exists(_lastExportedFolder))
                {
                    Process.Start("explorer.exe", _lastExportedFolder);
                }
            };
            pnlExportBtns.Controls.Add(_btnOpenFolder);
            pnlBottom.Controls.Add(pnlExportBtns);

            // Initialize System Tray
            InitializeTray();
        }

        private void InitializeTray()
        {
            _trayContextMenu = new ContextMenuStrip();

            ToolStripMenuItem menuOpen = new ToolStripMenuItem("🖥️ Open StreamClipMarker");
            menuOpen.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            menuOpen.Click += (s, e) => ShowFromTray();

            _trayMenuAction = new ToolStripMenuItem("⏱️ Start Session");
            _trayMenuAction.Click += (s, e) => BtnSessionAction_Click(s, e);

            ToolStripMenuItem menuMark = new ToolStripMenuItem("🎬 Mark Clip (F8)");
            menuMark.Click += (s, e) => TriggerClipMark(false, false);

            ToolStripMenuItem menuSettings = new ToolStripMenuItem("⚙️ Settings");
            menuSettings.Click += BtnSettings_Click;

            ToolStripMenuItem menuExit = new ToolStripMenuItem("❌ Exit Application");
            menuExit.Click += (s, e) => ExitApplication();

            _trayContextMenu.Items.Add(menuOpen);
            _trayContextMenu.Items.Add(_trayMenuAction);
            _trayContextMenu.Items.Add(menuMark);
            _trayContextMenu.Items.Add(new ToolStripSeparator());
            _trayContextMenu.Items.Add(menuSettings);
            _trayContextMenu.Items.Add(new ToolStripSeparator());
            _trayContextMenu.Items.Add(menuExit);

            _trayIcon = new NotifyIcon
            {
                Text = "StreamClipMarker",
                Icon = AppBranding.GetAppIcon(),
                ContextMenuStrip = _trayContextMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
            UpdateTrayTooltip();
        }

        private void UpdateTrayTooltip()
        {
            if (_trayIcon == null) return;
            string stateStr = (_state == SessionState.Active) ? "ACTIVE" :
                              (_state == SessionState.WaitingForRecording) ? "WAITING" : "IDLE";
            int count = (_currentSession != null) ? _currentSession.Markers.Count : 0;

            // Keep within 63 characters for safety on all Windows versions
            string tip = string.Format("StreamClipMarker\nRec: {0} | Clips: {1}\nLast: {2}", stateStr, count, _lastMarkerFormatted);
            if (tip.Length > 63)
            {
                tip = string.Format("StreamClipMarker: {0}\nClips: {1}", stateStr, count);
            }
            try
            {
                _trayIcon.Text = tip;
            }
            catch { }
        }

        private void InitializeApp()
        {
            _config = AppConfig.Load();
            _timer = new SessionTimer();
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += (s, e) => HotkeyManager_HotkeyPressed(false);
            _hotkeyManager.MarkWithLabelPressed += (s, e) => HotkeyManager_HotkeyPressed(true);
            _toast = new ToastFeedback();

            _clockTimer = new Timer();
            _clockTimer.Interval = 1000;
            _clockTimer.Tick += ClockTimer_Tick;

            _countdownTimer = new Timer();
            _countdownTimer.Interval = 1000;
            _countdownTimer.Tick += CountdownTimer_Tick;

            // Auto-detection engine
            _recordingDetector = new RecordingDetector(_config.AutoDetectRecording, _config.WatchedRecordingFolder);
            _recordingDetector.RecordingStarted += RecordingDetector_RecordingStarted;
            _recordingDetector.RecordingStopped += RecordingDetector_RecordingStopped;
            _recordingDetector.Start();

            UpdateHotkeyBinding();
            UpdateOffsetDisplay();

            if (_config.AutoDetectRecording)
            {
                SetState(SessionState.WaitingForRecording);
            }
            else
            {
                SetState(SessionState.Idle);
            }

            // Enhanced Crash Recovery Check (Resume / Finalize / Delete)
            CheckForUnfinishedSession();
        }

        private void RecordingDetector_RecordingStarted(object sender, RecordingEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnAutoRecordingStarted(e)));
            }
            else
            {
                OnAutoRecordingStarted(e);
            }
        }

        private void RecordingDetector_RecordingStopped(object sender, RecordingEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnAutoRecordingStopped(e)));
            }
            else
            {
                OnAutoRecordingStopped(e);
            }
        }

        private void OnAutoRecordingStarted(RecordingEventArgs e)
        {
            if (_state == SessionState.Idle || _state == SessionState.WaitingForRecording || _state == SessionState.Ended)
            {
                _lblRecordingFile.Text = string.IsNullOrEmpty(e.Details) ? "" : "File: " + e.Details;
                BeginLiveSession();

                string detailMsg = string.Format("Auto-Started Session! ({0})", e.Details);
                if (_config.EnableToast)
                {
                    _toast.ShowToast("● RECORDING DETECTED", detailMsg, _config.EnableSound);
                }

                if (!Visible)
                {
                    _trayIcon.ShowBalloonTip(2000, "StreamClipMarker",
                        "Recording detected! Session auto-started. Press " + _config.GetHotkeyDisplayString() + " anytime to mark clips.",
                        ToolTipIcon.Info);
                }
            }
        }

        private void OnAutoRecordingStopped(RecordingEventArgs e)
        {
            if (!_config.AutoEndRecording) return;

            if (_state == SessionState.Active && _currentSession != null)
            {
                SetState(SessionState.Finalizing);
                int markerCount = _currentSession.Markers.Count;
                EndSession(true);

                string detailMsg = string.Format("Recording finished ({0}). Saved {1} clip(s)!", e.Details, markerCount);
                if (_config.EnableToast)
                {
                    _toast.ShowToast("✓ SESSION SAVED", detailMsg, _config.EnableSound);
                }

                if (!Visible)
                {
                    _trayIcon.ShowBalloonTip(3000, "StreamClipMarker", detailMsg, ToolTipIcon.Info);
                }
            }
        }

        private void CheckForUnfinishedSession()
        {
            if (SessionStorage.HasUnfinishedSession())
            {
                SessionData recovered = SessionStorage.LoadCurrentSession();
                if (recovered != null && recovered.Markers.Count > 0)
                {
                    using (SessionRecoveryForm recoveryForm = new SessionRecoveryForm(recovered))
                    {
                        recoveryForm.ShowDialog(this);
                        if (recoveryForm.UserAction == RecoveryAction.Resume)
                        {
                            // Resume previous session!
                            _currentSession = recovered;
                            _currentSession.IsActive = true;
                            _timer.Reset();
                            _timer.StartWithOffset(recovered.DurationSeconds);

                            RefreshMarkersList();
                            _lblClipsCount.Text = string.Format("CLIPS MARKED: {0}", _currentSession.Markers.Count);
                            SetState(SessionState.Active);
                            ClockTimer_Tick(this, EventArgs.Empty);

                            if (_config.EnableToast)
                            {
                                _toast.ShowToast("SESSION RESUMED", string.Format("Resumed with {0} saved clips.", recovered.Markers.Count), false);
                            }
                        }
                        else if (recoveryForm.UserAction == RecoveryAction.Finalize)
                        {
                            _currentSession = recovered;
                            FinalizeCurrentSession(false);
                        }
                        else if (recoveryForm.UserAction == RecoveryAction.Delete)
                        {
                            SessionStorage.DeleteCurrentSession();
                        }
                    }
                }
                else
                {
                    SessionStorage.DeleteCurrentSession();
                }
            }
        }

        private void UpdateHotkeyBinding()
        {
            _hotkeyManager.Register(_config.HotkeyKey, _config.HotkeyCtrl, _config.HotkeyAlt, _config.HotkeyShift);
            _btnMarkClip.Text = string.Format("MARK CLIP — {0}", _config.GetHotkeyDisplayString());
            UpdateTrayTooltip();
        }

        private void UpdateOffsetDisplay()
        {
            int offset = _config.RecordingOffsetSeconds;
            _lblOffsetVal.Text = string.Format("{0}{1}s", offset >= 0 ? "+" : "", offset);
            if (_currentSession != null)
            {
                _currentSession.RecordingOffsetSeconds = offset;
                RefreshMarkersList();
            }
        }

        private void AdjustOffset(int delta)
        {
            _config.RecordingOffsetSeconds += delta;
            _config.Save();
            UpdateOffsetDisplay();
            if (_currentSession != null && _state == SessionState.Active)
            {
                SessionStorage.SaveCurrentSession(_currentSession);
            }
        }

        private void SetState(SessionState state)
        {
            _state = state;
            switch (_state)
            {
                case SessionState.Idle:
                    _lblStatus.Text = "○ IDLE";
                    _lblStatus.ForeColor = Color.FromArgb(130, 140, 155);
                    _btnSessionAction.Text = "START SESSION";
                    _btnSessionAction.BackColor = Color.FromArgb(0, 130, 230);
                    _btnSessionAction.Visible = true;
                    _btnMarkClip.Enabled = true;
                    _lblClock.Text = "00:00:00";
                    _lblRecordingFile.Text = "";
                    _clockTimer.Stop();
                    _countdownTimer.Stop();
                    if (_trayMenuAction != null) _trayMenuAction.Text = "⏱️ Start Session";
                    break;

                case SessionState.WaitingForRecording:
                    _lblStatus.Text = "○ WAITING FOR RECORDING";
                    _lblStatus.ForeColor = Color.FromArgb(240, 190, 60);
                    _btnSessionAction.Text = "START SESSION MANUALLY";
                    _btnSessionAction.BackColor = Color.FromArgb(45, 95, 160);
                    _btnSessionAction.Visible = true;
                    _btnMarkClip.Enabled = true;
                    _lblClock.Text = "00:00:00";
                    _clockTimer.Stop();
                    _countdownTimer.Stop();
                    if (_trayMenuAction != null) _trayMenuAction.Text = "⏱️ Start Session Manually";
                    break;

                case SessionState.Countdown:
                    _lblStatus.Text = string.Format("⏳ STARTING IN {0}...", _countdownRemaining);
                    _lblStatus.ForeColor = Color.FromArgb(240, 180, 40);
                    _btnSessionAction.Text = "CANCEL COUNTDOWN";
                    _btnSessionAction.BackColor = Color.FromArgb(180, 80, 50);
                    _countdownTimer.Start();
                    if (_trayMenuAction != null) _trayMenuAction.Text = "Cancel Countdown";
                    break;

                case SessionState.Active:
                    _lblStatus.Text = "● RECORDING DETECTED";
                    _lblStatus.ForeColor = Color.FromArgb(0, 220, 130);
                    _btnSessionAction.Text = "END SESSION";
                    _btnSessionAction.BackColor = Color.FromArgb(200, 40, 40);
                    _clockTimer.Start();
                    if (_trayMenuAction != null) _trayMenuAction.Text = "🛑 End Session";
                    break;

                case SessionState.Finalizing:
                    _lblStatus.Text = "◐ FINALIZING SESSION";
                    _lblStatus.ForeColor = Color.FromArgb(255, 140, 40);
                    _btnSessionAction.Text = "SAVING...";
                    _btnSessionAction.BackColor = Color.FromArgb(80, 80, 90);
                    _clockTimer.Stop();
                    break;

                case SessionState.Ended:
                    _lblStatus.Text = "✓ SESSION SAVED";
                    _lblStatus.ForeColor = Color.FromArgb(0, 200, 240);
                    _btnSessionAction.Text = "START NEW SESSION";
                    _btnSessionAction.BackColor = Color.FromArgb(0, 130, 230);
                    _clockTimer.Stop();
                    if (_trayMenuAction != null) _trayMenuAction.Text = "⏱️ Start New Session";
                    break;
            }
            UpdateTrayTooltip();
        }

        private void BtnSessionAction_Click(object sender, EventArgs e)
        {
            switch (_state)
            {
                case SessionState.Idle:
                case SessionState.WaitingForRecording:
                    StartSessionSequence();
                    break;

                case SessionState.Countdown:
                    _countdownTimer.Stop();
                    SetState(_config.AutoDetectRecording ? SessionState.WaitingForRecording : SessionState.Idle);
                    break;

                case SessionState.Active:
                    EndSession(false);
                    break;

                case SessionState.Ended:
                    StartSessionSequence();
                    break;
            }
        }

        private void StartSessionSequence()
        {
            if (_config.EnableCountdown && _config.CountdownSeconds > 0)
            {
                _countdownRemaining = _config.CountdownSeconds;
                SetState(SessionState.Countdown);
            }
            else
            {
                BeginLiveSession();
            }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _countdownRemaining--;
            if (_countdownRemaining <= 0)
            {
                _countdownTimer.Stop();
                BeginLiveSession();
            }
            else
            {
                _lblStatus.Text = string.Format("⏳ STARTING IN {0}...", _countdownRemaining);
            }
        }

        private void BeginLiveSession()
        {
            _timer.Reset();
            _timer.Start();

            _currentSession = new SessionData
            {
                SessionStartLocal = DateTime.Now,
                SessionStartUtc = DateTime.UtcNow,
                RecordingOffsetSeconds = _config.RecordingOffsetSeconds,
                PaddingBeforeSeconds = _config.PaddingBeforeSeconds,
                PaddingAfterSeconds = _config.PaddingAfterSeconds,
                IsActive = true
            };

            _lstMarkers.Items.Clear();
            _lblClipsCount.Text = "CLIPS MARKED: 0";
            _lastMarkerRawSeconds = -999;
            _lastMarkerFormatted = "--:--:--";

            // Immediately persist session so it's crash-proof from the very first moment
            SessionStorage.SaveCurrentSession(_currentSession);

            SetState(SessionState.Active);
            ClockTimer_Tick(this, EventArgs.Empty);
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            if (_timer.IsRunning)
            {
                _lblClock.Text = _timer.ElapsedFormatted;
                if (_currentSession != null)
                {
                    _currentSession.DurationSeconds = _timer.ElapsedTotalSeconds;
                }
            }
        }

        private void HotkeyManager_HotkeyPressed(bool requestLabel)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => TriggerClipMark(false, requestLabel)));
            }
            else
            {
                TriggerClipMark(false, requestLabel);
            }
        }

        private void TriggerClipMark(bool clickedFromUI, bool requestLabel)
        {
            if (_state != SessionState.Active || _currentSession == null)
            {
                if (clickedFromUI)
                {
                    MessageBox.Show(this, "Please click 'START SESSION' before marking clips.", "No Active Session",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            // CAPTURE TIMESTAMP INSTANTANEOUSLY (no delay, zero interruption!)
            double rawSec = _timer.ElapsedTotalSeconds;

            bool rapidPress = (_lastMarkerRawSeconds >= 0 && (rawSec - _lastMarkerRawSeconds) < 2.0);
            _lastMarkerRawSeconds = rawSec;

            int markerId = _currentSession.Markers.Count + 1;
            ClipMarker marker = new ClipMarker(markerId, rawSec);
            _currentSession.Markers.Add(marker);
            _currentSession.DurationSeconds = rawSec;

            // Save immediately for crash safety
            SessionStorage.SaveCurrentSession(_currentSession);

            // Update UI list & tray
            string timeStr = marker.GetTimestamp(_currentSession.RecordingOffsetSeconds);
            _lastMarkerFormatted = timeStr;
            AddMarkerToUI(marker);
            _lblClipsCount.Text = string.Format("CLIPS MARKED: {0}", _currentSession.Markers.Count);
            UpdateTrayTooltip();

            string rangeStr = string.Format("Clip: {0} → {1}",
                marker.GetClipStart(_currentSession.RecordingOffsetSeconds, _currentSession.PaddingBeforeSeconds),
                marker.GetClipEnd(_currentSession.RecordingOffsetSeconds, _currentSession.PaddingAfterSeconds));

            if (rapidPress)
            {
                rangeStr += " (rapid press recorded)";
            }

            // Non-blocking toast feedback
            if (_config.EnableToast)
            {
                _toast.ShowToast(string.Format("CLIP MARKED — {0}", timeStr), rangeStr, _config.EnableSound);
            }
            else if (_config.EnableSound)
            {
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }

            // If Ctrl+F8 was pressed, allow entering an optional label without blocking the timestamp
            if (requestLabel)
            {
                BeginInvoke(new Action(() =>
                {
                    string input = ShowInputDialog(
                        string.Format("Enter label for Clip #{0} ({1}):", marker.Id, timeStr),
                        "Clip Label", marker.Note);
                    if (input != null)
                    {
                        marker.Note = input.Trim();
                        SessionStorage.SaveCurrentSession(_currentSession);
                        RefreshMarkersList();
                    }
                }));
            }
        }

        private void AddMarkerToUI(ClipMarker m)
        {
            string markerTime = m.GetTimestamp(_currentSession.RecordingOffsetSeconds);
            string range = string.Format("{0} - {1}",
                m.GetClipStart(_currentSession.RecordingOffsetSeconds, _currentSession.PaddingBeforeSeconds),
                m.GetClipEnd(_currentSession.RecordingOffsetSeconds, _currentSession.PaddingAfterSeconds));

            ListViewItem item = new ListViewItem(m.Id.ToString());
            item.SubItems.Add(markerTime);
            item.SubItems.Add(range);
            item.SubItems.Add(m.Note);
            item.Tag = m;

            _lstMarkers.Items.Add(item);
            item.EnsureVisible();
        }

        private void RefreshMarkersList()
        {
            _lstMarkers.Items.Clear();
            if (_currentSession == null) return;

            for (int i = 0; i < _currentSession.Markers.Count; i++)
            {
                ClipMarker m = _currentSession.Markers[i];
                m.Id = i + 1;
                AddMarkerToUI(m);
            }
            _lblClipsCount.Text = string.Format("CLIPS MARKED: {0}", _currentSession.Markers.Count);
        }

        private void EndSession(bool isAutomatic = false)
        {
            _timer.Stop();
            _clockTimer.Stop();

            if (_recordingDetector != null)
            {
                _recordingDetector.NotifySessionEndedManually();
            }

            if (_currentSession != null)
            {
                _currentSession.DurationSeconds = _timer.ElapsedTotalSeconds;
                _currentSession.IsActive = false;
                FinalizeCurrentSession(isAutomatic);
            }

            SetState(SessionState.Ended);

            // If auto-detect enabled, return to waiting for next recording
            if (_config.AutoDetectRecording)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(s =>
                {
                    System.Threading.Thread.Sleep(2000);
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => { if (_state == SessionState.Ended) SetState(SessionState.WaitingForRecording); }));
                    }
                });
            }
        }

        private void FinalizeCurrentSession(bool isAutomatic = false)
        {
            if (_currentSession == null) return;

            SessionExportResult res = SessionStorage.FinalizeSession(_currentSession, _config.OutputFolder);
            _lastExportedTxtPath = res.TxtFilePath;
            _lastExportedFolder = _config.OutputFolder;

            // Show export action buttons
            foreach (Control c in Controls)
            {
                if (c is Panel && c.Dock == DockStyle.Bottom)
                {
                    foreach (Control child in c.Controls)
                    {
                        if (child is Panel) child.Visible = true;
                    }
                }
            }

            if (!isAutomatic && Visible)
            {
                MessageBox.Show(this,
                    string.Format("Livestream session saved successfully!\n\nMarkers: {0}\nDuration: {1}\n\nFiles created in:\n{2}",
                    _currentSession.Markers.Count,
                    SessionTimer.FormatTime(_currentSession.DurationSeconds),
                    _lastExportedTxtPath),
                    "Session Finalized", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _trayIcon.ShowBalloonTip(3000, "Session Finalized",
                    string.Format("Saved {0} clip markers to TXT, JSON & CSV in Documents!", _currentSession.Markers.Count),
                    ToolTipIcon.Info);
            }
        }

        public void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _isExiting = true;
            if (_state == SessionState.Active && _currentSession != null && _currentSession.Markers.Count > 0)
            {
                DialogResult dr = MessageBox.Show(this,
                    "A session is currently running!\n\nDo you want to finalize and save your session before exiting?",
                    "Session in Progress", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (dr == DialogResult.Yes)
                {
                    EndSession(false);
                }
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            Application.Exit();
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using (SettingsForm sf = new SettingsForm(_config, _recordingDetector))
            {
                if (sf.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateHotkeyBinding();
                    UpdateOffsetDisplay();

                    if (_recordingDetector != null)
                    {
                        _recordingDetector.Stop();
                        _recordingDetector.Enabled = _config.AutoDetectRecording;
                        _recordingDetector.WatchedFolder = _config.WatchedRecordingFolder;
                        _recordingDetector.Start();
                    }

                    if (_state == SessionState.Idle && _config.AutoDetectRecording)
                    {
                        SetState(SessionState.WaitingForRecording);
                    }
                    else if (_state == SessionState.WaitingForRecording && !_config.AutoDetectRecording)
                    {
                        SetState(SessionState.Idle);
                    }
                }
            }
        }

        private void MenuEdit_Click(object sender, EventArgs e)
        {
            if (_lstMarkers.SelectedItems.Count == 0) return;
            ListViewItem item = _lstMarkers.SelectedItems[0];
            ClipMarker m = item.Tag as ClipMarker;
            if (m == null) return;

            string currentNote = m.Note;
            string input = ShowInputDialog("Edit Marker Note / Label:", "Edit Marker", currentNote);
            if (input != null)
            {
                m.Note = input.Trim();
                item.SubItems[3].Text = m.Note;
                if (_currentSession != null)
                {
                    SessionStorage.SaveCurrentSession(_currentSession);
                }
            }
        }

        private void MenuDelete_Click(object sender, EventArgs e)
        {
            if (_lstMarkers.SelectedItems.Count == 0 || _currentSession == null) return;
            ListViewItem item = _lstMarkers.SelectedItems[0];
            ClipMarker m = item.Tag as ClipMarker;
            if (m == null) return;

            _currentSession.Markers.Remove(m);
            SessionStorage.SaveCurrentSession(_currentSession);
            RefreshMarkersList();
        }

        private void MenuAddManual_Click(object sender, EventArgs e)
        {
            if (_currentSession == null || _state != SessionState.Active)
            {
                MessageBox.Show(this, "Start a session before adding markers.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string input = ShowInputDialog("Enter timestamp (HH:MM:SS or MM:SS):", "Add Manual Marker", _timer.ElapsedFormatted);
            if (!string.IsNullOrEmpty(input))
            {
                double seconds = SessionTimer.ParseTime(input);
                int markerId = _currentSession.Markers.Count + 1;
                ClipMarker marker = new ClipMarker(markerId, seconds, "Manual");
                _currentSession.Markers.Add(marker);
                _currentSession.Markers.Sort((a, b) => a.RawSeconds.CompareTo(b.RawSeconds));

                SessionStorage.SaveCurrentSession(_currentSession);
                RefreshMarkersList();
            }
        }

        private void MenuClear_Click(object sender, EventArgs e)
        {
            if (_currentSession == null || _currentSession.Markers.Count == 0) return;

            if (MessageBox.Show(this, "Are you sure you want to clear all markers for this session?",
                "Clear Markers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _currentSession.Markers.Clear();
                SessionStorage.SaveCurrentSession(_currentSession);
                RefreshMarkersList();
            }
        }

        private string ShowInputDialog(string text, string caption, string defaultVal)
        {
            Form prompt = new Form()
            {
                Width = 380,
                Height = 165,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(28, 30, 38),
                ForeColor = Color.White,
                MaximizeBox = false,
                MinimizeBox = false
            };
            try { prompt.Icon = Icon; } catch { }

            Label textLabel = new Label() { Left = 20, Top = 16, Text = text, AutoSize = true, ForeColor = Color.LightGray };
            TextBox textBox = new TextBox() { Left = 20, Top = 42, Width = 320, Text = defaultVal, BackColor = Color.FromArgb(40, 42, 50), ForeColor = Color.White };
            Button confirmation = new Button() { Text = "Save", Left = 170, Width = 80, Top = 80, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0, 140, 90), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button cancel = new Button() { Text = "Cancel", Left = 260, Width = 80, Top = 80, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(50, 52, 60), ForeColor = Color.LightGray, FlatStyle = FlatStyle.Flat };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isExiting && _config.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();

                if (!_hasShownTrayBalloon)
                {
                    _hasShownTrayBalloon = true;
                    _trayIcon.ShowBalloonTip(2000, "StreamClipMarker",
                        "Running quietly in taskbar tray. Press " + _config.GetHotkeyDisplayString() + " anytime to mark clips, or right-click to exit.",
                        ToolTipIcon.Info);
                }
                return;
            }

            if (_recordingDetector != null)
            {
                _recordingDetector.Dispose();
            }
            if (_hotkeyManager != null)
            {
                _hotkeyManager.Dispose();
            }
            if (_toast != null)
            {
                _toast.Dispose();
            }
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            base.OnFormClosing(e);
        }
    }
}
