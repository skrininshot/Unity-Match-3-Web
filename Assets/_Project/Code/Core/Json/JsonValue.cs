using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Match3.Core.Json
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    public sealed class JsonException : Exception
    {
        public JsonException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// A tiny JSON reader/writer.
    /// <para>
    /// Written by hand rather than using Unity's JsonUtility for two reasons: this assembly is
    /// engine-free by design, and level data is recursive (a crate can contain a crate), which
    /// JsonUtility does not handle. Everything is mapped explicitly, so it also survives IL2CPP
    /// stripping in the WebGL build without any attributes.
    /// </para>
    /// All parsing and formatting uses the invariant culture — on a machine with a comma decimal
    /// separator, culture-sensitive number handling would silently corrupt every level file.
    /// </summary>
    public sealed class JsonValue
    {
        private static readonly JsonValue NullValue = new JsonValue { Kind = JsonKind.Null };

        public JsonKind Kind { get; private set; }
        public bool Bool { get; private set; }
        public double Number { get; private set; }
        public string Text { get; private set; }
        public List<JsonValue> Items { get; private set; }
        public Dictionary<string, JsonValue> Members { get; private set; }

        // ------------------------------------------------------------------ construction

        public static JsonValue Null() => NullValue;

        public static JsonValue Of(bool value) => new JsonValue { Kind = JsonKind.Bool, Bool = value };

        public static JsonValue Of(double value) => new JsonValue { Kind = JsonKind.Number, Number = value };

        public static JsonValue Of(int value) => new JsonValue { Kind = JsonKind.Number, Number = value };

        public static JsonValue Of(string value) =>
            value == null ? NullValue : new JsonValue { Kind = JsonKind.String, Text = value };

        public static JsonValue Array() =>
            new JsonValue { Kind = JsonKind.Array, Items = new List<JsonValue>() };

        public static JsonValue Object() =>
            new JsonValue { Kind = JsonKind.Object, Members = new Dictionary<string, JsonValue>() };

        public JsonValue Add(JsonValue item)
        {
            if (Kind != JsonKind.Array)
                throw new JsonException($"Cannot add an item to a JSON {Kind}.");
            Items.Add(item);
            return this;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object)
                throw new JsonException($"Cannot set a member on a JSON {Kind}.");
            Members[key] = value;
            return this;
        }

        public JsonValue Set(string key, string value) => Set(key, Of(value));
        public JsonValue Set(string key, int value) => Set(key, Of(value));
        public JsonValue Set(string key, bool value) => Set(key, Of(value));

        // ------------------------------------------------------------------ access

        public bool Has(string key) => Kind == JsonKind.Object && Members.ContainsKey(key);

        public JsonValue this[string key] =>
            Kind == JsonKind.Object && Members.TryGetValue(key, out JsonValue value) ? value : NullValue;

        public JsonValue this[int index] =>
            Kind == JsonKind.Array && index >= 0 && index < Items.Count ? Items[index] : NullValue;

        public int Count => Kind == JsonKind.Array ? Items.Count : 0;

        public bool IsNull => Kind == JsonKind.Null;

        public string AsString(string fallback = null) => Kind == JsonKind.String ? Text : fallback;

        public int AsInt(int fallback = 0) =>
            Kind == JsonKind.Number ? (int)System.Math.Round(Number) : fallback;

        public double AsDouble(double fallback = 0) => Kind == JsonKind.Number ? Number : fallback;

        public bool AsBool(bool fallback = false) => Kind == JsonKind.Bool ? Bool : fallback;

        public IEnumerable<JsonValue> AsArray()
        {
            if (Kind != JsonKind.Array)
                yield break;

            foreach (JsonValue item in Items)
                yield return item;
        }

        // ------------------------------------------------------------------ writing

        public string ToJson(bool pretty = true)
        {
            var sb = new StringBuilder();
            Write(sb, pretty, 0);
            return sb.ToString();
        }

        public override string ToString() => ToJson(false);

        private void Write(StringBuilder sb, bool pretty, int depth)
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;

                case JsonKind.Bool:
                    sb.Append(Bool ? "true" : "false");
                    break;

                case JsonKind.Number:
                    WriteNumber(sb, Number);
                    break;

                case JsonKind.String:
                    WriteString(sb, Text);
                    break;

                case JsonKind.Array:
                    WriteArray(sb, pretty, depth);
                    break;

                case JsonKind.Object:
                    WriteObject(sb, pretty, depth);
                    break;
            }
        }

        private void WriteArray(StringBuilder sb, bool pretty, int depth)
        {
            if (Items.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            // Arrays of scalars stay on one line; that keeps a layout readable as a picture.
            bool inline = !pretty || AllScalars();

            sb.Append('[');
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0)
                    sb.Append(inline ? ", " : ",");
                if (!inline)
                    NewLine(sb, depth + 1);
                Items[i].Write(sb, pretty, depth + 1);
            }

            if (!inline)
                NewLine(sb, depth);
            sb.Append(']');
        }

        private void WriteObject(StringBuilder sb, bool pretty, int depth)
        {
            if (Members.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, JsonValue> member in Members)
            {
                if (!first)
                    sb.Append(',');
                first = false;

                if (pretty)
                    NewLine(sb, depth + 1);

                WriteString(sb, member.Key);
                sb.Append(pretty ? ": " : ":");
                member.Value.Write(sb, pretty, depth + 1);
            }

            if (pretty)
                NewLine(sb, depth);
            sb.Append('}');
        }

        private bool AllScalars()
        {
            foreach (JsonValue item in Items)
                if (item.Kind == JsonKind.Array || item.Kind == JsonKind.Object)
                    return false;
            return true;
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static void WriteNumber(StringBuilder sb, double value)
        {
            if (value == System.Math.Floor(value) && !double.IsInfinity(value))
                sb.Append(((long)value).ToString(CultureInfo.InvariantCulture));
            else
                sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        // ------------------------------------------------------------------ parsing

        public static JsonValue Parse(string text)
        {
            if (text == null)
                throw new JsonException("Cannot parse null JSON.");

            int index = 0;
            JsonValue value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);

            if (index != text.Length)
                throw new JsonException($"Unexpected trailing content at offset {index}.");

            return value;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
                throw new JsonException("Unexpected end of JSON.");

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return Of(ParseString(s, ref i));
                case 't': Expect(s, ref i, "true"); return Of(true);
                case 'f': Expect(s, ref i, "false"); return Of(false);
                case 'n': Expect(s, ref i, "null"); return Null();
                default: return Of(ParseNumber(s, ref i));
            }
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            JsonValue result = Object();
            i++; // '{'
            SkipWhitespace(s, ref i);

            if (i < s.Length && s[i] == '}')
            {
                i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"')
                    throw new JsonException($"Expected a member name at offset {i}.");

                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);

                if (i >= s.Length || s[i] != ':')
                    throw new JsonException($"Expected ':' after member '{key}' at offset {i}.");
                i++;

                result.Members[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);

                if (i >= s.Length)
                    throw new JsonException("Unterminated JSON object.");

                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (s[i] == '}')
                {
                    i++;
                    return result;
                }

                throw new JsonException($"Expected ',' or '}}' at offset {i}.");
            }
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            JsonValue result = Array();
            i++; // '['
            SkipWhitespace(s, ref i);

            if (i < s.Length && s[i] == ']')
            {
                i++;
                return result;
            }

            while (true)
            {
                result.Items.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);

                if (i >= s.Length)
                    throw new JsonException("Unterminated JSON array.");

                if (s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (s[i] == ']')
                {
                    i++;
                    return result;
                }

                throw new JsonException($"Expected ',' or ']' at offset {i}.");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();

            while (true)
            {
                if (i >= s.Length)
                    throw new JsonException("Unterminated JSON string.");

                char c = s[i++];
                if (c == '"')
                    return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i >= s.Length)
                    throw new JsonException("Unterminated escape sequence.");

                char escape = s[i++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                            throw new JsonException("Truncated \\u escape.");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default:
                        throw new JsonException($"Unknown escape '\\{escape}'.");
                }
            }
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
                i++;

            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                    || s[i] == '-' || s[i] == '+'))
                i++;

            string token = s.Substring(start, i - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new JsonException($"'{token}' is not a valid number (offset {start}).");

            return value;
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
                throw new JsonException($"Expected '{literal}' at offset {i}.");
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    i++;
                else
                    break;
            }
        }
    }
}
