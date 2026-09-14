using System;
using System.Diagnostics;
using System.Globalization;

namespace StreamClipMarker.Core
{
    public class SessionTimer
    {
        private readonly Stopwatch _stopwatch;
        private double _initialOffsetSeconds;

        public SessionTimer()
        {
            _stopwatch = new Stopwatch();
            _initialOffsetSeconds = 0;
        }

        public bool IsRunning
        {
            get { return _stopwatch.IsRunning; }
        }

        public void Start()
        {
            _stopwatch.Start();
        }

        public void StartWithOffset(double offsetSeconds)
        {
            _initialOffsetSeconds = offsetSeconds;
            _stopwatch.Start();
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }

        public void Reset()
        {
            _stopwatch.Reset();
            _initialOffsetSeconds = 0;
        }

        public double ElapsedTotalSeconds
        {
            get
            {
                return _initialOffsetSeconds + _stopwatch.Elapsed.TotalSeconds;
            }
        }

        public string ElapsedFormatted
        {
            get
            {
                return FormatTime(ElapsedTotalSeconds);
            }
        }

        public static string FormatTime(double totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;

            long totalSec = (long)Math.Floor(totalSeconds);
            long hours = totalSec / 3600;
            long minutes = (totalSec % 3600) / 60;
            long seconds = totalSec % 60;

            return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
        }

        public static double ParseTime(string formatted)
        {
            if (string.IsNullOrEmpty(formatted)) return 0;
            string[] parts = formatted.Trim().Split(':');
            if (parts.Length == 3)
            {
                double h, m, s;
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out h) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                {
                    return (h * 3600.0) + (m * 60.0) + s;
                }
            }
            else if (parts.Length == 2)
            {
                double m, s;
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out m) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                {
                    return (m * 60.0) + s;
                }
            }
            return 0;
        }
    }
}
