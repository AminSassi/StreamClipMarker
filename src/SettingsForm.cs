using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StreamClipMarker.Core
{
    public class SettingsForm : Form
    {
        private readonly AppConfig _config;

        private ComboBox _comboHotkey;
        private CheckBox _chkCtrl;
        private CheckBox _chkAlt;
        private CheckBox _chkShift;

        private NumericUpDown _numPaddingBefore;
        private NumericUpDown _numPaddingAfter;
        private NumericUpDown _numOffset;

        private CheckBox _chkCountdown;
        private TextBox _txtOutputFolder;
        private Button _btnBrowseFolder;

        private CheckBox _chkSound;
        private CheckBox _chkToast;

        // New controls
        private CheckBox _chkAutoDetect;
        private TextBox _txtWatchedFolder;
        private Button _btnBrowseWatched;
        private CheckBox _chkMinimizeToTray;

        private Button _btnSave;
        private Button _btnCancel;

        public SettingsForm(AppConfig config)
        {
            _config = config;
            InitializeUI();
            LoadConfigValues();
        }

        private void InitializeUI()
        {
            Text = "StreamClipMarker Settings";
            Size = new Size(480, 640);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(28, 30, 36);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            try
            {
                Icon = AppBranding.GetAppIcon();
            }
            catch { }

            int y = 16;

            // Section: Global Hotkey
            Label lblHotkeySection = CreateHeaderLabel("GLOBAL HOTKEY", 20, y);
            Controls.Add(lblHotkeySection);
            y += 26;

            Label lblKey = new Label { Text = "Key:", Location = new Point(25, y + 3), AutoSize = true, ForeColor = Color.LightGray };
            Controls.Add(lblKey);

            _comboHotkey = new ComboBox
            {
                Location = new Point(70, y),
                Width = 90,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 42, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _comboHotkey.Items.AddRange(new object[] { "F6", "F7", "F8", "F9", "F10", "F11", "F12" });
            Controls.Add(_comboHotkey);

            _chkCtrl = new CheckBox { Text = "Ctrl", Location = new Point(180, y + 2), AutoSize = true, ForeColor = Color.LightGray };
            _chkAlt = new CheckBox { Text = "Alt", Location = new Point(235, y + 2), AutoSize = true, ForeColor = Color.LightGray };
            _chkShift = new CheckBox { Text = "Shift", Location = new Point(285, y + 2), AutoSize = true, ForeColor = Color.LightGray };
            Controls.Add(_chkCtrl);
            Controls.Add(_chkAlt);
            Controls.Add(_chkShift);
            y += 38;

            // Section: Auto-Detection
            Label lblAutoSection = CreateHeaderLabel("AUTOMATIC LIVESTREAM / RECORDING DETECTION", 20, y);
            Controls.Add(lblAutoSection);
            y += 24;

            _chkAutoDetect = new CheckBox
            {
                Text = "Automatically start session when recording/stream begins",
                Location = new Point(25, y),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold)
            };
            Controls.Add(_chkAutoDetect);
            y += 24;

            Label lblWatchHint = new Label
            {
                Text = "Watched video folder (TikTok LIVE Studio / OBS / Screen Recorders):",
                Location = new Point(25, y),
                Width = 420,
                Height = 18,
                ForeColor = Color.DarkGray,
                Font = new Font("Segoe UI", 8.25f)
            };
            Controls.Add(lblWatchHint);
            y += 20;

            _txtWatchedFolder = new TextBox
            {
                Location = new Point(25, y),
                Width = 325,
                BackColor = Color.FromArgb(40, 42, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_txtWatchedFolder);

            _btnBrowseWatched = new Button
            {
                Text = "Browse...",
                Location = new Point(358, y - 1),
                Width = 80,
                Height = 25,
                BackColor = Color.FromArgb(48, 52, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnBrowseWatched.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = _txtWatchedFolder.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtWatchedFolder.Text = fbd.SelectedPath;
                    }
                }
            };
            Controls.Add(_btnBrowseWatched);
            y += 36;

            // Section: Clip Padding
            Label lblPaddingSection = CreateHeaderLabel("DEFAULT CLIP PADDING", 20, y);
            Controls.Add(lblPaddingSection);
            y += 24;

            Label lblPadBefore = new Label { Text = "Before marker:", Location = new Point(25, y + 3), AutoSize = true, ForeColor = Color.LightGray };
            Controls.Add(lblPadBefore);

            _numPaddingBefore = new NumericUpDown
            {
                Location = new Point(130, y),
                Width = 70,
                Minimum = 0,
                Maximum = 600,
                BackColor = Color.FromArgb(40, 42, 50),
                ForeColor = Color.White
            };
            Controls.Add(_numPaddingBefore);

            Label lblSecBefore = new Label { Text = "sec", Location = new Point(205, y + 3), AutoSize = true, ForeColor = Color.Gray };
            Controls.Add(lblSecBefore);

            Label lblPadAfter = new Label { Text = "After marker:", Location = new Point(245, y + 3), AutoSize = true, ForeColor = Color.LightGray };
            Controls.Add(lblPadAfter);

            _numPaddingAfter = new NumericUpDown
            {
                Location = new Point(340, y),
                Width = 70,
                Minimum = 0,
                Maximum = 600,
                BackColor = Color.FromArgb(40, 42, 50),
                ForeColor = Color.White
            };
            Controls.Add(_numPaddingAfter);

            Label lblSecAfter = new Label { Text = "sec", Location = new Point(415, y + 3), AutoSize = true, ForeColor = Color.Gray };
            Controls.Add(lblSecAfter);
            y += 36;

            // Section: Recording Offset
            Label lblOffsetSection = CreateHeaderLabel("RECORDING TIME OFFSET", 20, y);
            Controls.Add(lblOffsetSection);
            y += 24;

            Label lblOffset = new Label { Text = "Manual Offset:", Location = new Point(25, y + 3), AutoSize = true, ForeColor = Color.LightGray };
            Controls.Add(lblOffset);

            _numOffset = new NumericUpDown
            {
                Location = new Point(130, y),
                Width = 85,
                Minimum = -3600,
                Maximum = 3600,
                BackColor = Color.FromArgb(40, 42, 50),
                ForeColor = Color.White
            };
            Controls.Add(_numOffset);

            Label lblSecOffset = new Label { Text = "sec  (can also adjust live during/after session)", Location = new Point(220, y + 3), AutoSize = true, ForeColor = Color.Gray };
            Controls.Add(lblSecOffset);
            y += 36;

            // Section: Session & Taskbar Options
            Label lblSessionSection = CreateHeaderLabel("TASKBAR & BEHAVIOR", 20, y);
            Controls.Add(lblSessionSection);
            y += 24;

            _chkMinimizeToTray = new CheckBox
            {
                Text = "Close button [X] minimizes silently to system tray (right taskbar)",
                Location = new Point(25, y),
                AutoSize = true,
                ForeColor = Color.LightGray
            };
            Controls.Add(_chkMinimizeToTray);
            y += 24;

            _chkCountdown = new CheckBox
            {
                Text = "3-2-1 Countdown before session timer starts (when manual)",
                Location = new Point(25, y),
                AutoSize = true,
                ForeColor = Color.LightGray
            };
            Controls.Add(_chkCountdown);
            y += 24;

            _chkSound = new CheckBox
            {
                Text = "Play subtle beep sound when clip is marked",
                Location = new Point(25, y),
                AutoSize = true,
                ForeColor = Color.LightGray
            };
            Controls.Add(_chkSound);
            y += 24;

            _chkToast = new CheckBox
            {
                Text = "Show non-intrusive on-screen notification (disappears in 1s)",
                Location = new Point(25, y),
                AutoSize = true,
                ForeColor = Color.LightGray
            };
            Controls.Add(_chkToast);
            y += 36;

            // Section: Output Folder
            Label lblOutputSection = CreateHeaderLabel("OUTPUT EXPORT FOLDER", 20, y);
            Controls.Add(lblOutputSection);
            y += 24;

            _txtOutputFolder = new TextBox
            {
                Location = new Point(25, y),
                Width = 325,
                BackColor = Color.FromArgb(40, 42, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_txtOutputFolder);

            _btnBrowseFolder = new Button
            {
                Text = "Browse...",
                Location = new Point(358, y - 1),
                Width = 80,
                Height = 25,
                BackColor = Color.FromArgb(48, 52, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnBrowseFolder.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = _txtOutputFolder.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtOutputFolder.Text = fbd.SelectedPath;
                    }
                }
            };
            Controls.Add(_btnBrowseFolder);
            y += 42;

            // Action Buttons
            _btnSave = new Button
            {
                Text = "Save Changes",
                Location = new Point(218, y),
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(0, 160, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;
            Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(348, y),
                Width = 90,
                Height = 34,
                BackColor = Color.FromArgb(50, 52, 60),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            Controls.Add(_btnCancel);
        }

        private Label CreateHeaderLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 192, 128)
            };
        }

        private void LoadConfigValues()
        {
            if (_comboHotkey.Items.Contains(_config.HotkeyKey))
            {
                _comboHotkey.SelectedItem = _config.HotkeyKey;
            }
            else
            {
                _comboHotkey.SelectedIndex = 2; // F8
            }

            _chkCtrl.Checked = _config.HotkeyCtrl;
            _chkAlt.Checked = _config.HotkeyAlt;
            _chkShift.Checked = _config.HotkeyShift;

            _chkAutoDetect.Checked = _config.AutoDetectRecording;
            _txtWatchedFolder.Text = _config.WatchedRecordingFolder;
            _chkMinimizeToTray.Checked = _config.MinimizeToTrayOnClose;

            _numPaddingBefore.Value = Math.Max(0, Math.Min(600, _config.PaddingBeforeSeconds));
            _numPaddingAfter.Value = Math.Max(0, Math.Min(600, _config.PaddingAfterSeconds));
            _numOffset.Value = Math.Max(-3600, Math.Min(3600, _config.RecordingOffsetSeconds));

            _chkCountdown.Checked = _config.EnableCountdown;
            _chkSound.Checked = _config.EnableSound;
            _chkToast.Checked = _config.EnableToast;

            _txtOutputFolder.Text = _config.OutputFolder;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            _config.HotkeyKey = _comboHotkey.SelectedItem != null ? _comboHotkey.SelectedItem.ToString() : "F8";
            _config.HotkeyCtrl = _chkCtrl.Checked;
            _config.HotkeyAlt = _chkAlt.Checked;
            _config.HotkeyShift = _chkShift.Checked;

            _config.AutoDetectRecording = _chkAutoDetect.Checked;
            _config.WatchedRecordingFolder = _txtWatchedFolder.Text.Trim();
            _config.MinimizeToTrayOnClose = _chkMinimizeToTray.Checked;

            _config.PaddingBeforeSeconds = (int)_numPaddingBefore.Value;
            _config.PaddingAfterSeconds = (int)_numPaddingAfter.Value;
            _config.RecordingOffsetSeconds = (int)_numOffset.Value;

            _config.EnableCountdown = _chkCountdown.Checked;
            _config.EnableSound = _chkSound.Checked;
            _config.EnableToast = _chkToast.Checked;

            string folder = _txtOutputFolder.Text.Trim();
            if (!string.IsNullOrEmpty(folder))
            {
                _config.OutputFolder = folder;
            }

            _config.Save();
            DialogResult = DialogResult.OK;
        }
    }
}
