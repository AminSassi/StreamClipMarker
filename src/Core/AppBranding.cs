using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StreamClipMarker.Core
{
    public static class AppBranding
    {
        private static Icon _appIcon;

        public static Icon GetAppIcon()
        {
            if (_appIcon != null) return _appIcon;

            try
            {
                // First attempt: extract icon embedded in the executable
                string exePath = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Icon exeIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (exeIcon != null)
                    {
                        _appIcon = exeIcon;
                        return _appIcon;
                    }
                }
            }
            catch { }

            try
            {
                // Second attempt: load from app_icon.ico next to executable or working dir
                string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(localIco))
                {
                    _appIcon = new Icon(localIco);
                    return _appIcon;
                }
            }
            catch { }

            try
            {
                // Fallback: draw a programmatic icon (32x32)
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(22, 25, 34)))
                    {
                        g.FillEllipse(bg, 1, 1, 30, 30);
                    }
                    using (Pen ring = new Pen(Color.FromArgb(0, 230, 138), 2.5f))
                    {
                        g.DrawEllipse(ring, 2, 2, 27, 27);
                    }
                    using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 51, 102)))
                    {
                        g.FillEllipse(dot, 10, 10, 12, 12);
                    }
                }
                IntPtr hIcon = bmp.GetHicon();
                _appIcon = Icon.FromHandle(hIcon);
                return _appIcon;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }
}
