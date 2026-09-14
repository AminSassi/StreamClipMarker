using System;
using System.Drawing;
using System.Windows.Forms;
using StreamClipMarker.Core;

namespace StreamClipMarker
{
    public enum RecoveryAction
    {
        Resume,
        Finalize,
        Delete,
        Cancel
    }

    public class SessionRecoveryForm : Form
    {
        public RecoveryAction UserAction { get; private set; }

        public SessionRecoveryForm(SessionData session)
        {
            UserAction = RecoveryAction.Cancel;
            InitializeUI(session);
        }

        private void InitializeUI(SessionData session)
        {
            Text = "StreamClipMarker - Unfinished Session Found";
            Size = new Size(460, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 26, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            try
            {
                Icon = AppBranding.GetAppIcon();
            }
            catch { }

            // Header Banner
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(32, 34, 44),
                Padding = new Padding(16, 12, 16, 12)
            };

            Label lblTitle = new Label
            {
                Text = "⚠️ Unfinished Session Detected",
                Location = new Point(14, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 190, 60)
            };
            pnlHeader.Controls.Add(lblTitle);

            Label lblSub = new Label
            {
                Text = "A session was running when the app or computer was closed.",
                Location = new Point(16, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.LightGray
            };
            pnlHeader.Controls.Add(lblSub);
            Controls.Add(pnlHeader);

            // Info Details
            Panel pnlInfo = new Panel
            {
                Location = new Point(16, 72),
                Size = new Size(412, 80),
                BackColor = Color.FromArgb(30, 32, 40)
            };

            Label lblDetails = new Label
            {
                Text = string.Format(
                    "Started:    {0:yyyy-MM-dd HH:mm:ss}\n" +
                    "Duration:   {1}\n" +
                    "Markers:    {2} clip(s) saved",
                    session.SessionStartLocal,
                    SessionTimer.FormatTime(session.DurationSeconds),
                    session.Markers.Count),
                Location = new Point(14, 12),
                AutoSize = true,
                Font = new Font("Consolas", 10f),
                ForeColor = Color.FromArgb(220, 230, 245)
            };
            pnlInfo.Controls.Add(lblDetails);
            Controls.Add(pnlInfo);

            // Preview List
            ListView lstPreview = new ListView
            {
                Location = new Point(16, 160),
                Size = new Size(412, 110),
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.FromArgb(210, 220, 235),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f)
            };
            lstPreview.Columns.Add("#", 36);
            lstPreview.Columns.Add("Marker", 80);
            lstPreview.Columns.Add("Clip Range", 160);
            lstPreview.Columns.Add("Note", 110);

            for (int i = 0; i < session.Markers.Count; i++)
            {
                ClipMarker m = session.Markers[i];
                ListViewItem lvi = new ListViewItem((i + 1).ToString());
                lvi.SubItems.Add(m.GetTimestamp(session.RecordingOffsetSeconds));
                lvi.SubItems.Add(string.Format("{0} - {1}",
                    m.GetClipStart(session.RecordingOffsetSeconds, session.PaddingBeforeSeconds),
                    m.GetClipEnd(session.RecordingOffsetSeconds, session.PaddingAfterSeconds)));
                lvi.SubItems.Add(m.Note);
                lstPreview.Items.Add(lvi);
            }
            Controls.Add(lstPreview);

            // Bottom Buttons
            Panel pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(20, 21, 26)
            };

            // RESUME BUTTON
            Button btnResume = new Button
            {
                Text = "RESUME",
                Location = new Point(16, 10),
                Size = new Size(125, 34),
                BackColor = Color.FromArgb(0, 160, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResume.FlatAppearance.BorderSize = 0;
            btnResume.Click += (s, e) =>
            {
                UserAction = RecoveryAction.Resume;
                DialogResult = DialogResult.OK;
                Close();
            };
            pnlButtons.Controls.Add(btnResume);

            // FINALIZE BUTTON
            Button btnFinalize = new Button
            {
                Text = "FINALIZE",
                Location = new Point(150, 10),
                Size = new Size(130, 34),
                BackColor = Color.FromArgb(0, 130, 220),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnFinalize.FlatAppearance.BorderSize = 0;
            btnFinalize.Click += (s, e) =>
            {
                UserAction = RecoveryAction.Finalize;
                DialogResult = DialogResult.Yes;
                Close();
            };
            pnlButtons.Controls.Add(btnFinalize);

            // DELETE BUTTON
            Button btnDelete = new Button
            {
                Text = "DELETE",
                Location = new Point(290, 10),
                Size = new Size(138, 34),
                BackColor = Color.FromArgb(160, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Are you sure you want to discard these markers?",
                    "Confirm Discard", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    UserAction = RecoveryAction.Delete;
                    DialogResult = DialogResult.Abort;
                    Close();
                }
            };
            pnlButtons.Controls.Add(btnDelete);

            Controls.Add(pnlButtons);
        }
    }
}
