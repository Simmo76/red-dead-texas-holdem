using System.Globalization;
using System.Text;

namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// Minimal, culture-invariant JSON serialisation for analytics events —
    /// one event per line (JSON Lines), so the on-device log can be appended
    /// atomically, tailed, imported into any analytics tool, or replayed into
    /// a vendor SDK later. No external JSON library: the engine stays
    /// dependency-free and Unity/netstandard-compatible.
    /// </summary>
    public static class AnalyticsJson
    {
        public static string Serialize(AnalyticsEvent evt)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            AppendString(sb, "name", evt.Name);
            sb.Append(',');
            AppendString(sb, "ts", evt.UtcTimestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            sb.Append(',');
            AppendString(sb, "player_id", evt.PlayerId);
            sb.Append(',');
            AppendString(sb, "session_id", evt.SessionId);
            sb.Append(',');
            sb.Append("\"seq\":").Append(evt.SequenceNumber.ToString(CultureInfo.InvariantCulture));

            if (evt.Parameters.Count > 0)
            {
                sb.Append(",\"params\":{");
                bool first = true;
                foreach (var pair in evt.Parameters)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    AppendEscaped(sb, pair.Key);
                    sb.Append(':');
                    AppendValue(sb, pair.Value);
                }
                sb.Append('}');
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            AppendEscaped(sb, key);
            sb.Append(':');
            AppendEscaped(sb, value);
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                default:
                    AppendEscaped(sb, value.ToString());
                    break;
            }
        }

        private static void AppendEscaped(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
