using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class PlayerProgressCodec
{
    public const int SupportedSchemaVersion = 1;

    public static string Serialize(
        IReadOnlyList<string> discoveredIds)
    {
        PlayerProgressData data = new PlayerProgressData
        {
            schemaVersion = SupportedSchemaVersion,
            discoveredIds = NormalizeIds(discoveredIds)
        };

        return JsonUtility.ToJson(data, true);
    }

    public static List<string> NormalizeIds(
        IReadOnlyList<string> discoveredIds)
    {
        List<string> normalized = new List<string>();
        HashSet<string> seen = new HashSet<string>();

        if (discoveredIds == null)
        {
            return normalized;
        }

        for (int index = 0; index < discoveredIds.Count; index++)
        {
            string id = discoveredIds[index];

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (seen.Add(id))
            {
                normalized.Add(id);
            }
        }

        return normalized;
    }

    public static ProgressParseResult Parse(string json)
    {
        try
        {
            List<string> rawIds = ReadDocument(json);
            List<string> normalized = NormalizeIds(rawIds);

            if (!MatchesJsonUtility(json, normalized))
            {
                return ProgressParseResult.Failed(
                    ProgressFileProblem.InvalidStructure,
                    "Progress JSON did not match the supported schema.");
            }

            return ProgressParseResult.Valid(normalized);
        }
        catch (ProgressParseException exception)
        {
            return ProgressParseResult.Failed(
                exception.Problem,
                exception.Message);
        }
    }

    private static bool MatchesJsonUtility(
        string json,
        IReadOnlyList<string> normalizedIds)
    {
        PlayerProgressData data =
            JsonUtility.FromJson<PlayerProgressData>(json);

        if (data == null ||
            data.schemaVersion != SupportedSchemaVersion)
        {
            return false;
        }

        List<string> utilityIds =
            NormalizeIds(data.discoveredIds);

        if (utilityIds.Count != normalizedIds.Count)
        {
            return false;
        }

        for (int index = 0; index < normalizedIds.Count; index++)
        {
            if (!string.Equals(
                    utilityIds[index],
                    normalizedIds[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> ReadDocument(string json)
    {
        JsonText text = new JsonText(json);
        text.SkipWhitespace();

        if (text.Peek() != '{')
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidJson,
                "Progress JSON must be an object.");
        }

        ParsedObject root = text.ReadObject();
        text.SkipWhitespace();

        if (!text.End)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidJson,
                "Progress JSON has trailing content.");
        }

        if (root.SchemaRaw == null)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "schemaVersion is missing.");
        }

        ProgressFileProblem schemaProblem =
            ClassifySchema(root.SchemaRaw);

        if (schemaProblem ==
            ProgressFileProblem.UnsupportedSchema)
        {
            throw new ProgressParseException(
                ProgressFileProblem.UnsupportedSchema,
                "schemaVersion is newer than supported version " +
                SupportedSchemaVersion + ".");
        }

        if (schemaProblem != ProgressFileProblem.None)
        {
            throw new ProgressParseException(
                schemaProblem,
                "schemaVersion is not supported version " +
                SupportedSchemaVersion + ".");
        }

        if (root.IdsRaw == null)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "discoveredIds is missing.");
        }

        return ReadIds(root.IdsRaw);
    }

    private static ProgressFileProblem ClassifySchema(
        string rawValue)
    {
        JsonText text = new JsonText(rawValue);
        text.SkipWhitespace();

        if (!text.TryReadNumber(out string token))
        {
            return ProgressFileProblem.InvalidStructure;
        }

        text.SkipWhitespace();

        if (!text.End)
        {
            return ProgressFileProblem.InvalidJson;
        }

        if (IsCanonicalInteger(token) &&
            long.TryParse(
                token,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long integer))
        {
            if (integer == SupportedSchemaVersion)
            {
                return ProgressFileProblem.None;
            }

            if (integer > SupportedSchemaVersion)
            {
                return ProgressFileProblem.UnsupportedSchema;
            }

            return ProgressFileProblem.InvalidStructure;
        }

        if (double.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number) &&
            number > SupportedSchemaVersion)
        {
            return ProgressFileProblem.UnsupportedSchema;
        }

        return ProgressFileProblem.InvalidStructure;
    }

    private static bool IsCanonicalInteger(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        int start = token[0] == '-' ? 1 : 0;

        if (start >= token.Length)
        {
            return false;
        }

        for (int index = start; index < token.Length; index++)
        {
            if (!char.IsDigit(token[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> ReadIds(string rawValue)
    {
        JsonText text = new JsonText(rawValue);
        text.SkipWhitespace();

        if (text.Peek() != '[')
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "discoveredIds must be a list of strings.");
        }

        List<string> ids = text.ReadIdArray();
        text.SkipWhitespace();

        if (!text.End)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidJson,
                "discoveredIds has trailing content.");
        }

        return ids;
    }

    private sealed class ProgressParseException : Exception
    {
        public ProgressFileProblem Problem { get; }

        public ProgressParseException(
            ProgressFileProblem problem,
            string detail) : base(detail)
        {
            Problem = problem;
        }
    }

    private sealed class ParsedObject
    {
        public string SchemaRaw;
        public string IdsRaw;
    }

    private sealed class JsonText
    {
        private const int MaximumDepth = 32;
        private readonly string text;
        private int index;

        public JsonText(string value)
        {
            text = value ?? string.Empty;

            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                index = 1;
            }
        }

        public bool End => index >= text.Length;

        public void SkipWhitespace()
        {
            while (index < text.Length)
            {
                char character = text[index];

                if (character != ' ' &&
                    character != '\t' &&
                    character != '\n' &&
                    character != '\r')
                {
                    break;
                }

                index++;
            }
        }

        public char Peek()
        {
            if (index >= text.Length)
            {
                return '\0';
            }

            return text[index];
        }

        public ParsedObject ReadObject()
        {
            return ReadObject(0);
        }

        public List<string> ReadIdArray()
        {
            Expect('[');
            SkipWhitespace();

            List<string> ids = new List<string>();

            if (Consume(']'))
            {
                return ids;
            }

            while (true)
            {
                SkipWhitespace();
                ids.Add(ReadIdElement());
                SkipWhitespace();

                if (Consume(','))
                {
                    continue;
                }

                Expect(']');
                break;
            }

            return ids;
        }

        public bool TryReadNumber(out string token)
        {
            SkipWhitespace();
            int start = index;

            if (!TryConsumeNumber())
            {
                index = start;
                token = null;
                return false;
            }

            token = text.Substring(start, index - start);
            return true;
        }

        private ParsedObject ReadObject(int depth)
        {
            EnsureDepth(depth);
            Expect('{');
            SkipWhitespace();

            ParsedObject parsed = new ParsedObject();

            if (Consume('}'))
            {
                return parsed;
            }

            while (true)
            {
                SkipWhitespace();
                string key = ReadString();
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                string rawValue = CaptureRawValue(depth + 1);

                if (key == "schemaVersion")
                {
                    if (parsed.SchemaRaw != null)
                    {
                        throw new ProgressParseException(
                            ProgressFileProblem.InvalidStructure,
                            "schemaVersion is duplicated.");
                    }

                    parsed.SchemaRaw = rawValue;
                }
                else if (key == "discoveredIds")
                {
                    if (parsed.IdsRaw != null)
                    {
                        throw new ProgressParseException(
                            ProgressFileProblem.InvalidStructure,
                            "discoveredIds is duplicated.");
                    }

                    parsed.IdsRaw = rawValue;
                }

                SkipWhitespace();

                if (Consume(','))
                {
                    continue;
                }

                Expect('}');
                break;
            }

            return parsed;
        }

        private string ReadIdElement()
        {
            char character = Peek();

            if (character == '"')
            {
                return ReadString();
            }

            if (character == 'n' && ConsumeLiteral("null"))
            {
                return null;
            }

            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "discoveredIds must contain only strings.");
        }

        private string CaptureRawValue(int depth)
        {
            int start = index;
            SkipValue(depth);
            return text.Substring(start, index - start);
        }

        private void SkipValue(int depth)
        {
            EnsureDepth(depth);
            SkipWhitespace();
            char character = Peek();

            if (character == '"')
            {
                ReadString();
                return;
            }

            if (character == '{')
            {
                ReadObject(depth);
                return;
            }

            if (character == '[')
            {
                SkipArray(depth);
                return;
            }

            if (character == 't')
            {
                ExpectLiteral("true");
                return;
            }

            if (character == 'f')
            {
                ExpectLiteral("false");
                return;
            }

            if (character == 'n')
            {
                ExpectLiteral("null");
                return;
            }

            if (character == '-' || char.IsDigit(character))
            {
                if (!TryConsumeNumber())
                {
                    throw new ProgressParseException(
                        ProgressFileProblem.InvalidJson,
                        "Progress JSON has an invalid number.");
                }

                return;
            }

            throw new ProgressParseException(
                ProgressFileProblem.InvalidJson,
                "Progress JSON has an unexpected value.");
        }

        private void SkipArray(int depth)
        {
            EnsureDepth(depth);
            Expect('[');
            SkipWhitespace();

            if (Consume(']'))
            {
                return;
            }

            while (true)
            {
                SkipValue(depth + 1);
                SkipWhitespace();

                if (Consume(','))
                {
                    continue;
                }

                Expect(']');
                break;
            }
        }

        private string ReadString()
        {
            if (!Consume('"'))
            {
                throw new ProgressParseException(
                    ProgressFileProblem.InvalidJson,
                    "Progress JSON has an invalid string.");
            }

            StringBuilder builder = new StringBuilder();

            while (index < text.Length)
            {
                char character = text[index++];

                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character == '\\')
                {
                    builder.Append(ReadEscape());
                    continue;
                }

                if (character < 0x20)
                {
                    throw new ProgressParseException(
                        ProgressFileProblem.InvalidJson,
                        "Progress JSON has a raw control character.");
                }

                builder.Append(character);
            }

            throw new ProgressParseException(
                ProgressFileProblem.InvalidJson,
                "Progress JSON has an unterminated string.");
        }

        private char ReadEscape()
        {
            if (index >= text.Length)
            {
                throw new ProgressParseException(
                    ProgressFileProblem.InvalidJson,
                    "Progress JSON has an invalid escape.");
            }

            char character = text[index++];

            switch (character)
            {
                case '"':
                case '\\':
                case '/':
                    return character;
                case 'b':
                    return '\b';
                case 'f':
                    return '\f';
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                case 'u':
                    return ReadUnicodeEscape();
                default:
                    throw new ProgressParseException(
                        ProgressFileProblem.InvalidJson,
                        "Progress JSON has an invalid escape.");
            }
        }

        private char ReadUnicodeEscape()
        {
            if (index + 4 > text.Length)
            {
                throw new ProgressParseException(
                    ProgressFileProblem.InvalidJson,
                    "Progress JSON has an invalid unicode escape.");
            }

            int value = 0;

            for (int offset = 0; offset < 4; offset++)
            {
                char character = text[index++];
                int digit = HexValue(character);

                if (digit < 0)
                {
                    throw new ProgressParseException(
                        ProgressFileProblem.InvalidJson,
                        "Progress JSON has an invalid unicode escape.");
                }

                value = (value << 4) + digit;
            }

            return (char)value;
        }

        private static int HexValue(char character)
        {
            if (character >= '0' && character <= '9')
            {
                return character - '0';
            }

            if (character >= 'a' && character <= 'f')
            {
                return character - 'a' + 10;
            }

            if (character >= 'A' && character <= 'F')
            {
                return character - 'A' + 10;
            }

            return -1;
        }

        private bool TryConsumeNumber()
        {
            int start = index;

            if (Peek() == '-')
            {
                index++;
            }

            if (index >= text.Length || !char.IsDigit(text[index]))
            {
                index = start;
                return false;
            }

            if (text[index] == '0')
            {
                index++;
            }
            else
            {
                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            if (Peek() == '.')
            {
                index++;

                if (index >= text.Length || !char.IsDigit(text[index]))
                {
                    index = start;
                    return false;
                }

                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            char exponent = Peek();

            if (exponent == 'e' || exponent == 'E')
            {
                index++;

                if (Peek() == '+' || Peek() == '-')
                {
                    index++;
                }

                if (index >= text.Length || !char.IsDigit(text[index]))
                {
                    index = start;
                    return false;
                }

                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            return true;
        }

        private void ExpectLiteral(string literal)
        {
            if (!ConsumeLiteral(literal))
            {
                throw new ProgressParseException(
                    ProgressFileProblem.InvalidJson,
                    "Progress JSON has an unexpected value.");
            }
        }

        private bool ConsumeLiteral(string literal)
        {
            if (index + literal.Length > text.Length)
            {
                return false;
            }

            for (int offset = 0; offset < literal.Length; offset++)
            {
                if (text[index + offset] != literal[offset])
                {
                    return false;
                }
            }

            index += literal.Length;
            return true;
        }

        private void Expect(char expected)
        {
            if (!Consume(expected))
            {
                throw new ProgressParseException(
                    ProgressFileProblem.InvalidJson,
                    "Progress JSON is missing '" + expected + "'.");
            }
        }

        private bool Consume(char expected)
        {
            if (Peek() != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private void EnsureDepth(int depth)
        {
            if (depth > MaximumDepth)
            {
                throw new ProgressParseException(
                    ProgressFileProblem.InvalidJson,
                    "Progress JSON is nested too deeply.");
            }
        }
    }
}
