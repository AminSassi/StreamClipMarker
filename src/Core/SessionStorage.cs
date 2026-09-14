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
            txt.AppendLine("Session:");
            txt.AppendLine(session.SessionStartLocal.ToString("yyyy-MM-dd HH:mm:ss"));
            txt.AppendLine();
            txt.AppendLine("Duration:");
            txt.AppendLine(SessionTimer.FormatTime(session.DurationSeconds));
            txt.AppendLine();
            txt.AppendLine("Markers:");
            txt.AppendLine();

            if (session.Markers.Count == 0)
            {
                txt.AppendLine("  (No markers were recorded during this session)");
            }
            else
            {
                for (int i = 0; i < session.Markers.Count; i++)
                {
                    ClipMarker m = session.Markers[i];
                    string markerTime = m.GetTimestamp(session.RecordingOffsetSeconds);
                    string noteSuffix = string.IsNullOrEmpty(m.Note) ? "" : string.Format(" - {0}", m.Note);
                    txt.AppendLine(string.Format("{0:D2}. {1}{2}", i + 1, markerTime, noteSuffix));
                }
            }

            File.WriteAllText(txtPath, txt.ToString(), Encoding.UTF8);

            // 2. Build machine-readable JSON
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine(string.Format("  \"session_start\": \"{0:yyyy-MM-ddTHH:mm:ss}\",", session.SessionStartLocal));
            json.AppendLine(string.Format("  \"duration\": \"{0}\",", SessionTimer.FormatTime(session.DurationSeconds)));
            json.AppendLine("  \"markers\": [");

            for (int i = 0; i < session.Markers.Count; i++)
            {
                ClipMarker m = session.Markers[i];
                int seconds = (int)Math.Round(m.GetAdjustedSeconds(session.RecordingOffsetSeconds));

                json.AppendLine("    {");
                json.AppendLine(string.Format("      \"timestamp\": \"{0}\",", m.GetTimestamp(session.RecordingOffsetSeconds)));
                if (!string.IsNullOrEmpty(m.Note))
                {
                    json.AppendLine(string.Format("      \"seconds\": {0},", seconds));
                    json.AppendLine(string.Format("      \"note\": \"{0}\"", JsonHelper.Escape(m.Note)));
                }
                else
                {
                    json.AppendLine(string.Format("      \"seconds\": {0}", seconds));
                }

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
            bool anyNotes = false;
            for (int i = 0; i < session.Markers.Count; i++)
            {
                if (!string.IsNullOrEmpty(session.Markers[i].Note))
                {
                    anyNotes = true;
                    break;
                }
            }

            StringBuilder csv = new StringBuilder();
            if (anyNotes)
            {
                csv.AppendLine("Marker,Timestamp,Seconds,Note");
            }
            else
            {
                csv.AppendLine("Marker,Timestamp,Seconds");
            }

            for (int i = 0; i < session.Markers.Count; i++)
            {
                ClipMarker m = session.Markers[i];
                string markerTime = m.GetTimestamp(session.RecordingOffsetSeconds);
                int seconds = (int)Math.Round(m.GetAdjustedSeconds(session.RecordingOffsetSeconds));

                if (anyNotes)
                {
                    string note = EscapeCsv(m.Note);
                    csv.AppendLine(string.Format("{0},{1},{2},{3}", i + 1, markerTime, seconds, note));
                }
                else
                {
                    csv.AppendLine(string.Format("{0},{1},{2}", i + 1, markerTime, seconds));
                }
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
