using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StreamClipMarker.Core;

namespace StreamClipMarker
{
    public class DetectionTestForm : Form
    {
        private readonly RecordingDetector _detector;
        private TextBox _txtLog;
        private Label _lblStatus;

        public DetectionTestForm(RecordingDetector detector)
        {
            _detector = detector;
            InitializeUI();
            if (_detector != null)
            {
                _detector.DiagnosticLogged += Detector_DiagnosticLogged;
            }
        }

        private void InitializeUI()
        {
            Text = "Test Recording Detection - Diagnostics";
            Size = new Size(540, 420);
            MinimumSize = new Size(460, 320);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 26, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            try
            {
                Icon = AppBranding.GetAppIcon();
            }
            catch { }

            // Top Panel
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = Color.FromArgb(30, 32, 40),
                Padding = new Padding(12, 8, 12, 8)
            };

            Label lblTitle = new Label
            {
                Text = "🔍 Real-Time Stream / Recording Diagnostics",
                Location = new Point(12, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 140)
            };
            pnlTop.Controls.Add(lblTitle);

            _lblStatus = new Label
            {
                Text = string.Format("Watched Folder: {0}", _detector != null ? _detector.WatchedFolder : "N/A"),
                Location = new Point(14, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.DarkGray
            };
            pnlTop.Controls.Add(_lblStatus);
            Controls.Add(pnlTop);

            // Center Log TextBox
            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 25),
                ForeColor = Color.FromArgb(220, 230, 245),
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None
            };
            Controls.Add(_txtLog);

            // Bottom Actions Panel
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Color.FromArgb(26, 28, 35),
                Padding = new Padding(12, 6, 12, 6)
            };

            Button btnSimulate = new Button
            {
                Text = "Simulate Recording Event",
                Location = new Point(12, 6),
                Size = new Size(170, 30),
                BackColor = Color.FromArgb(42, 45, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f)
            };
            btnSimulate.FlatAppearance.BorderSize = 0;
            btnSimulate.Click += (s, e) => SimulateTestFile();
            pnlBottom.Controls.Add(btnSimulate);

            Button btnClear = new Button
            {
                Text = "Clear Log",
                Location = new Point(190, 6),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(42, 45, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f)
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => _txtLog.Clear();
            pnlBottom.Controls.Add(btnClear);

            Button btnClose = new Button
            {
                Text = "Close",
                Location = new Point(430, 6),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(50, 52, 62),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();
            pnlBottom.Controls.Add(btnClose);

            Controls.Add(pnlBottom);

            // Initial log entries
            AppendLog("Diagnostics window opened. Listening to recording detection events...");
            if (_detector != null)
            {
                AppendLog(string.Format("Current State: {0}", _detector.StateMachine.CurrentState));
                AppendLog("Start/stop recording in TikTok LIVE Studio, OBS, or Game Bar to test.");
            }
        }

        private void Detector_DiagnosticLogged(object sender, string log)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendLog(log)));
            }
            else
            {
                AppendLog(log);
            }
        }

        private void AppendLog(string message)
        {
            if (_txtLog.IsDisposed) return;
            _txtLog.AppendText(message + Environment.NewLine);
        }

        private void SimulateTestFile()
        {
            try
            {
                string folder = _detector != null ? _detector.WatchedFolder : Path.GetTempPath();
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                string testFile = Path.Combine(folder, string.Format("Test_Simulation_{0:HHmmss}.mp4", DateTime.Now));
                File.WriteAllText(testFile, "simulation header");
                AppendLog(string.Format("[TEST] Created test video file: {0}", Path.GetFileName(testFile)));

                // Clean up after 4 seconds
                System.Threading.ThreadPool.QueueUserWorkItem(s =>
                {
                    System.Threading.Thread.Sleep(4000);
                    try { if (File.Exists(testFile)) File.Delete(testFile); } catch { }
                });
            }
            catch (Exception ex)
            {
                AppendLog("[TEST ERROR] " + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_detector != null)
            {
                _detector.DiagnosticLogged -= Detector_DiagnosticLogged;
            }
            base.OnFormClosing(e);
        }
    }
}
