using System;

namespace StreamClipMarker.Core
{
    public class ClipMarker
    {
        public int Id { get; set; }
        public double RawSeconds { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public ClipMarker()
        {
            CreatedAtUtc = DateTime.UtcNow;
            Note = "";
        }

        public ClipMarker(int id, double rawSeconds, string note = "")
        {
            Id = id;
            RawSeconds = rawSeconds;
            Note = note ?? "";
            CreatedAtUtc = DateTime.UtcNow;
        }

        public double GetAdjustedSeconds(int recordingOffsetSeconds)
        {
            double adjusted = RawSeconds + recordingOffsetSeconds;
            return adjusted < 0 ? 0 : adjusted;
        }

        public string GetTimestamp(int recordingOffsetSeconds = 0)
        {
            return SessionTimer.FormatTime(GetAdjustedSeconds(recordingOffsetSeconds));
        }
    }
}
