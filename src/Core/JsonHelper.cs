using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamClipMarker.Core
{
    public static class JsonHelper
    {
        public static string Escape(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append(string.Format("\\u{0:x4}", (int)c));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string Unescape(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return Regex.Unescape(str);
        }

        public static string ExtractString(string json, string key, string defaultValue)
        {
            try
            {
                string pattern = string.Format("\"{0}\"\\s*:\\s*\"([^\"]*)\"", Regex.Escape(key));
                Match m = Regex.Match(json, pattern);
                if (m.Success)
                {
                    return Unescape(m.Groups[1].Value);
                }
            }
            catch { }
            return defaultValue;
        }

        public static int ExtractInt(string json, string key, int defaultValue)
        {
            try
            {
                string pattern = string.Format("\"{0}\"\\s*:\\s*(-?\\d+)", Regex.Escape(key));
                Match m = Regex.Match(json, pattern);
                if (m.Success)
                {
                    int val;
                    if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
                    {
                        return val;
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        public static double ExtractDouble(string json, string key, double defaultValue)
        {
            try
            {
                string pattern = string.Format("\"{0}\"\\s*:\\s*(-?\\d+(\\.\\d+)?)", Regex.Escape(key));
                Match m = Regex.Match(json, pattern);
                if (m.Success)
                {
                    double val;
                    if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                    {
                        return val;
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        public static bool ExtractBool(string json, string key, bool defaultValue)
        {
            try
            {
                string pattern = string.Format("\"{0}\"\\s*:\\s*(true|false)", Regex.Escape(key), RegexOptions.IgnoreCase);
                Match m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return defaultValue;
        }
    }
}
