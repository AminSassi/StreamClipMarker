using System;
using System.Collections.Generic;

namespace StreamClipMarker.Core
{
    public class SessionData
    {
        public DateTime SessionStartLocal { get; set; }
        public DateTime SessionStartUtc { get; set; }
        public double DurationSeconds { get; set; }
        public int RecordingOffsetSeconds { get; set; }
        public int PaddingBeforeSeconds { get; set; }
        public int PaddingAfterSeconds { get; set; }
        public bool IsActive { get; set; }
        public List<ClipMarker> Markers { get; set; }

        public SessionData()
        {
            SessionStartLocal = DateTime.Now;
            SessionStartUtc = DateTime.UtcNow;
            DurationSeconds = 0;
            RecordingOffsetSeconds = 0;
            PaddingBeforeSeconds = 10;
            PaddingAfterSeconds = 20;
            IsActive = false;
            Markers = new List<ClipMarker>();
        }
    }

    public class SessionExportResult
    {
        public string TxtFilePath { get; set; }
        public string JsonFilePath { get; set; }
    }
}
