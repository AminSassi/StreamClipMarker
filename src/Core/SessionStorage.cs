using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamClipMarker.Core
{
    public static class SessionStorage
    {
        public static string GetCurrentSessionPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "StreamClipMarker");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "current_session.json");
        }

        public static bool HasUnfinishedSession()
        {
            string path = GetCurrentSessionPath();
            return File.Exists(path);
        }

        public static void DeleteCurrentSession()
        {
            try
            {
                string path = GetCurrentSessionPath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }

        public static void SaveCurrentSession(SessionData session)
        {
            if (session == null) return;

            try
            {
                string targetPath = GetCurrentSessionPath();
                string tempPath = targetPath + ".tmp";

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"session_start_local\": \"{0:yyyy-MM-dd HH:mm:ss}\",", session.SessionStartLocal));
                sb.AppendLine(string.Format("  \"session_start_utc\": \"{0:o}\",", session.SessionStartUtc));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"duration_seconds\": {0:F3},", session.DurationSeconds));
                sb.AppendLine(string.Format("  \"recording_offset_seconds\": {0},", session.RecordingOffsetSeconds));
                sb.AppendLine(string.Format("  \"padding_before_seconds\": {0},", session.PaddingBeforeSeconds));
                sb.AppendLine(string.Format("  \"padding_after_seconds\": {0},", session.PaddingAfterSeconds));
                sb.AppendLine(string.Format("  \"is_active\": {0},", session.IsActive ? "true" : "false"));
                sb.AppendLine("  \"markers\": [");

                for (int i = 0; i < session.Markers.Count; i++)
                {
                    ClipMarker m = session.Markers[i];
                    sb.Append("    { ");
                    sb.Append(string.Format("\"id\": {0}, ", m.Id));
                    sb.Append(string.Format(CultureInfo.InvariantCulture, "\"raw_seconds\": {0:F3}, ", m.RawSeconds));
                    sb.Append(string.Format("\"timestamp\": \"{0}\", ", JsonHelper.Escape(m.GetTimestamp(session.RecordingOffsetSeconds))));
                    sb.Append(string.Format("\"note\": \"{0}\", ", JsonHelper.Escape(m.Note)));
                    sb.Append(string.Format("\"created_at_utc\": \"{0:o}\"", m.CreatedAtUtc));
                    sb.Append(" }");

                    if (i < session.Markers.Count - 1)
                    {
                        sb.AppendLine(",");
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(tempPath, sb.ToString(), Encoding.UTF8);

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(tempPath, targetPath);
            }
            catch
            {
                // Fallback attempt
            }
        }

        public static SessionData LoadCurrentSession()
        {
            string path = GetCurrentSessionPath();
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                SessionData session = new SessionData();

                string startUtcStr = JsonHelper.ExtractString(json, "session_start_utc", "");
                DateTime startUtc;
                if (DateTime.TryParse(startUtcStr, null, DateTimeStyles.RoundtripKind, out startUtc))
                {
                    session.SessionStartUtc = startUtc;
                    session.SessionStartLocal = startUtc.ToLocalTime();
                }

                session.DurationSeconds = JsonHelper.ExtractDouble(json, "duration_seconds", 0);
                session.RecordingOffsetSeconds = JsonHelper.ExtractInt(json, "recording_offset_seconds", 0);
                session.PaddingBeforeSeconds = JsonHelper.ExtractInt(json, "padding_before_seconds", 10);
                session.PaddingAfterSeconds = JsonHelper.ExtractInt(json, "padding_after_seconds", 20);
                session.IsActive = JsonHelper.ExtractBool(json, "is_active", false);

                // Parse markers
                session.Markers = new List<ClipMarker>();
                int markersStart = json.IndexOf("\"markers\"");
                if (markersStart >= 0)
                {
                    string sub = json.Substring(markersStart);
                    MatchCollection matches = Regex.Matches(sub, "\\{\\s*\"id\"\\s*:\\s*(\\d+)[^}]*\\}");
                    foreach (Match match in matches)
                    {
                        string block = match.Value;
                        int id = JsonHelper.ExtractInt(block, "id", session.Markers.Count + 1);
                        double rawSec = JsonHelper.ExtractDouble(block, "raw_seconds", 0);
                        string note = JsonHelper.ExtractString(block, "note", "");

                        ClipMarker marker = new ClipMarker(id, rawSec, note);
                        string createdStr = JsonHelper.ExtractString(block, "created_at_utc", "");
                        DateTime createdAt;
                        if (DateTime.TryParse(createdStr, null, DateTimeStyles.RoundtripKind, out createdAt))
                        {
                            marker.CreatedAtUtc = createdAt;
                        }
                        session.Markers.Add(marker);
                    }
                }

                return session;
            }
            catch
            {
                return null;
            }
        }

        public static SessionExportResult FinalizeSession(SessionData session, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string baseName = string.Format("TikTok_Stream_{0:yyyy-MM-dd_HH-mm-ss}", session.SessionStartLocal);
            string txtPath = Path.Combine(outputFolder, baseName + ".txt");
            string jsonPath = Path.Combine(outputFolder, baseName + ".json");
            string csvPath = Path.Combine(outputFolder, baseName + ".csv");
            string clipsCsvPath = Path.Combine(outputFolder, "clips.csv");

            // 1. Build human-readable TXT
            StringBuilder txt = new StringBuilder();
            txt.AppendLine("TikTok LIVE STREAM CLIP MARKERS");
            txt.AppendLine("================================");
            txt.AppendLine();
            txt.AppendLine("Session start:");
            txt.AppendLine(session.SessionStartLocal.ToString("yyyy-MM-dd HH:mm:ss"));
            txt.AppendLine();
            txt.AppendLine("Session duration:");
            txt.AppendLine(SessionTimer.FormatTime(session.DurationSeconds));
            txt.AppendLine();
            txt.AppendLine("Recording offset:");
            txt.AppendLine(string.Format("{0}{1}s", session.RecordingOffsetSeconds >= 0 ? "+" : "", session.RecordingOffsetSeconds));
            txt.AppendLine();
            txt.AppendLine("Clip padding:");
            txt.AppendLine(string.Format("Before: {0}s | After: {1}s", session.PaddingBeforeSeconds, session.PaddingAfterSeconds));
            txt.AppendLine();
            txt.AppendLine("Total markers:");
            txt.AppendLine(session.Markers.Count.ToString());
            txt.AppendLine();
            txt.AppendLine("Markers:");
            txt.AppendLine();

            if (session.Markers.Count == 0)
            {
                txt.AppendLine("  (No clips were marked during this session)");
            }
            else
            {
                for (int i = 0; i < session.Markers.Count; i++)
                {
                    ClipMarker m = session.Markers[i];
                    string markerTime = m.GetTimestamp(session.RecordingOffsetSeconds);
                    string clipStart = m.GetClipStart(session.RecordingOffsetSeconds, session.PaddingBeforeSeconds);
                    string clipEnd = m.GetClipEnd(session.RecordingOffsetSeconds, session.PaddingAfterSeconds);

                    string noteSuffix = string.IsNullOrEmpty(m.Note) ? "" : string.Format(" - \"{0}\"", m.Note);
                    txt.AppendLine(string.Format("{0:D2}. {1}{2}", i + 1, markerTime, noteSuffix));
                    txt.AppendLine(string.Format("    Clip start: {0}", clipStart));
                    txt.AppendLine(string.Format("    Clip end:   {0}", clipEnd));
                    txt.AppendLine();
                }
            }

            File.WriteAllText(txtPath, txt.ToString(), Encoding.UTF8);

            // 2. Build machine-readable JSON
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine(string.Format("  \"session_start\": \"{0:yyyy-MM-ddTHH:mm:ss}\",", session.SessionStartLocal));
            json.AppendLine(string.Format("  \"session_start_utc\": \"{0:o}\",", session.SessionStartUtc));
            json.AppendLine(string.Format("  \"duration\": \"{0}\",", SessionTimer.FormatTime(session.DurationSeconds)));
            json.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"duration_seconds\": {0:F3},", session.DurationSeconds));
            json.AppendLine(string.Format("  \"recording_offset_seconds\": {0},", session.RecordingOffsetSeconds));
            json.AppendLine(string.Format("  \"padding_before_seconds\": {0},", session.PaddingBeforeSeconds));
            json.AppendLine(string.Format("  \"padding_after_seconds\": {0},", session.PaddingAfterSeconds));
            json.AppendLine("  \"markers\": [");

            for (int i = 0; i < session.Markers.Count; i++)
            {
                ClipMarker m = session.Markers[i];
                double adjSec = m.GetAdjustedSeconds(session.RecordingOffsetSeconds);
                double startSec = m.GetClipStartSeconds(session.RecordingOffsetSeconds, session.PaddingBeforeSeconds);
                double endSec = m.GetClipEndSeconds(session.RecordingOffsetSeconds, session.PaddingAfterSeconds);

                json.AppendLine("    {");
                json.AppendLine(string.Format("      \"id\": {0},", m.Id));
                json.AppendLine(string.Format("      \"timestamp\": \"{0}\",", m.GetTimestamp(session.RecordingOffsetSeconds)));
                json.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"seconds\": {0:F1},", adjSec));
                json.AppendLine(string.Format("      \"clip_start\": \"{0}\",", m.GetClipStart(session.RecordingOffsetSeconds, session.PaddingBeforeSeconds)));
                json.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"clip_start_seconds\": {0:F1},", startSec));
                json.AppendLine(string.Format("      \"clip_end\": \"{0}\",", m.GetClipEnd(session.RecordingOffsetSeconds, session.PaddingAfterSeconds)));
                json.AppendLine(string.Format(CultureInfo.InvariantCulture, "      \"clip_end_seconds\": {0:F1},", endSec));
                json.AppendLine(string.Format("      \"raw_elapsed_seconds\": {0:F3},", m.RawSeconds));
                json.AppendLine(string.Format("      \"note\": \"{0}\"", JsonHelper.Escape(m.Note)));

                if (i < session.Markers.Count - 1)
                {
                    json.AppendLine("    },");
                }
                else
                {
                    json.AppendLine("    }");
                }
            }

            json.AppendLine("  ]");
            json.AppendLine("}");

            File.WriteAllText(jsonPath, json.ToString(), Encoding.UTF8);

            // 3. Build CSV export (clips.csv and session-specific CSV)
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Clip,Marker,Start,End,Note");

            for (int i = 0; i < session.Markers.Count; i++)
            {
                ClipMarker m = session.Markers[i];
                string markerTime = m.GetTimestamp(session.RecordingOffsetSeconds);
                string clipStart = m.GetClipStart(session.RecordingOffsetSeconds, session.PaddingBeforeSeconds);
                string clipEnd = m.GetClipEnd(session.RecordingOffsetSeconds, session.PaddingAfterSeconds);
                string note = EscapeCsv(m.Note);

                csv.AppendLine(string.Format("{0},{1},{2},{3},{4}", i + 1, markerTime, clipStart, clipEnd, note));
            }

            File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
            File.WriteAllText(clipsCsvPath, csv.ToString(), Encoding.UTF8);

            // Clean up session file
            DeleteCurrentSession();

            SessionExportResult result = new SessionExportResult();
            result.TxtFilePath = txtPath;
            result.JsonFilePath = jsonPath;
            result.CsvFilePath = csvPath;
            return result;
        }

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            {
                return "\"" + val.Replace("\"", "\"\"") + "\"";
            }
            return val;
        }
    }
}
