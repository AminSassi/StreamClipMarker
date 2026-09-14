using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace StreamClipMarker.Core
{
    public class ToastFeedback : Form
    {
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private readonly Timer _dismissTimer;
        private string _message = "";
        private string _subMessage = "";

        public ToastFeedback()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            Width = 320;
            Height = 65;
            BackColor = Color.FromArgb(24, 24, 30);
            DoubleBuffered = true;

            _dismissTimer = new Timer();
            _dismissTimer.Interval = 1200; // 1.2 seconds
            _dismissTimer.Tick += (s, e) =>
            {
                _dismissTimer.Stop();
                Hide();
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void ShowToast(string message, string subMessage = "", bool playSound = true)
        {
            _message = message;
            _subMessage = subMessage;

            // Position at top-right corner of the primary screen
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Right - Width - 24, screen.Top + 30);

            if (playSound)
            {
                try
                {
                    SystemSounds.Asterisk.Play();
                }
                catch { }
            }

            Invalidate();
            if (!Visible)
            {
                Show();
            }

            _dismissTimer.Stop();
            _dismissTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background border & fill
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GetRoundedRectangle(rect, 8))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(28, 30, 38)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(0, 192, 128), 2f)) // Accent green
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Draw icon/bullet
            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(0, 220, 130)))
            {
                g.FillEllipse(dotBrush, 16, 26, 12, 12);
            }

            // Draw Title / Message
            using (Font titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            {
                g.DrawString(_message, titleFont, titleBrush, new PointF(38, 12));
            }

            // Draw SubMessage / Range
            if (!string.IsNullOrEmpty(_subMessage))
            {
                using (Font subFont = new Font("Segoe UI", 9f, FontStyle.Regular))
                using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(170, 180, 195)))
                {
                    g.DrawString(_subMessage, subFont, subBrush, new PointF(39, 36));
                }
            }
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            // Top left
            path.AddArc(arc, 180, 90);
            // Top right
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            // Bottom right
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // Bottom left
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_dismissTimer != null)
                {
                    _dismissTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
