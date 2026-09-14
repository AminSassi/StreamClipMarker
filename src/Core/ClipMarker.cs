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

        public double GetClipStartSeconds(int recordingOffsetSeconds, int paddingBeforeSeconds)
        {
            double adjusted = GetAdjustedSeconds(recordingOffsetSeconds);
            double start = adjusted - paddingBeforeSeconds;
            return start < 0 ? 0 : start;
        }

        public string GetClipStart(int recordingOffsetSeconds, int paddingBeforeSeconds)
        {
            return SessionTimer.FormatTime(GetClipStartSeconds(recordingOffsetSeconds, paddingBeforeSeconds));
        }

        public double GetClipEndSeconds(int recordingOffsetSeconds, int paddingAfterSeconds)
        {
            double adjusted = GetAdjustedSeconds(recordingOffsetSeconds);
            return adjusted + paddingAfterSeconds;
        }

        public string GetClipEnd(int recordingOffsetSeconds, int paddingAfterSeconds)
        {
            return SessionTimer.FormatTime(GetClipEndSeconds(recordingOffsetSeconds, paddingAfterSeconds));
        }
    }
}
