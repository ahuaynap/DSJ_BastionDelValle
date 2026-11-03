using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class PlayerProgressCodec
{
    public const int LegacySchemaVersion = 1;
    public const int AudioSchemaVersion = 2;
    public const int SupportedSchemaVersion = 3;
    public const float DefaultMusicVolume = 0.35f;
    public const float DefaultEffectsVolume = 0.8f;

    public static string Serialize(
        IReadOnlyList<string> discoveredIds,
        float musicVolume,
        float effectsVolume)
    {
        return Serialize(
            discoveredIds,
            musicVolume,
            effectsVolume,
            CampaignSnapshot.Empty);
    }

    public static string Serialize(
        IReadOnlyList<string> discoveredIds,
        float musicVolume,
        float effectsVolume,
        CampaignSnapshot campaign)
    {
        PlayerProgressData data = new PlayerProgressData
        {
            schemaVersion = SupportedSchemaVersion,
            discoveredIds = NormalizeIds(discoveredIds),
            musicVolume = ClampVolume(musicVolume),
            effectsVolume = ClampVolume(effectsVolume),
            campaignStarted = campaign.Started,
            completedMaps = CopyIds(campaign.CompletedMaps),
            clearedTutorials = CopyIds(campaign.ClearedTutorials),
            evaluations = CopyIds(campaign.Evaluations)
        };

        return JsonUtility.ToJson(data, true);
    }

    public static List<string> CopyIds(IReadOnlyList<string> values)
    {
        return NormalizeIds(values);
    }

    public static float ClampVolume(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0f;
        }

        if (value < 0f)
        {
            return 0f;
        }

        if (value > 1f)
        {
            return 1f;
        }

        return value;
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
            ParsedDocument document = ReadDocument(json);
            List<string> normalized = NormalizeIds(document.Ids);

            if (document.Legacy)
            {
                if (!MatchesDiscoveries(
                        json,
                        LegacySchemaVersion,
                        normalized))
                {
                    return ProgressParseResult.Failed(
                        ProgressFileProblem.InvalidStructure,
                        "Progress JSON did not match schema 1.");
                }

                return ProgressParseResult.Valid(
                    normalized,
                    DefaultMusicVolume,
                    DefaultEffectsVolume,
                    true,
                    true,
                    CampaignSnapshot.Empty);
            }

            if (document.AudioSchema)
            {
                if (!MatchesAudio(
                        json,
                        normalized,
                        document.MusicVolume,
                        document.EffectsVolume))
                {
                    return ProgressParseResult.Failed(
                        ProgressFileProblem.InvalidStructure,
                        "Progress JSON did not match schema 2.");
                }

                return ProgressParseResult.Valid(
                    normalized,
                    document.MusicVolume,
                    document.EffectsVolume,
                    false,
                    true,
                    CampaignSnapshot.Empty);
            }

            if (!MatchesCurrent(
                    json,
                    normalized,
                    document.MusicVolume,
                    document.EffectsVolume,
                    document.Campaign))
            {
                return ProgressParseResult.Failed(
                    ProgressFileProblem.InvalidStructure,
                    "Progress JSON did not match the supported schema.");
            }

            return ProgressParseResult.Valid(
                normalized,
                document.MusicVolume,
                document.EffectsVolume,
                false,
                false,
                document.Campaign);
        }
        catch (ProgressParseException exception)
        {
            return ProgressParseResult.Failed(
                exception.Problem,
                exception.Message);
        }
    }

    private static bool MatchesDiscoveries(
        string json,
        int expectedSchema,
        IReadOnlyList<string> normalizedIds)
    {
        PlayerProgressData data =
            JsonUtility.FromJson<PlayerProgressData>(json);

        if (data == null ||
            data.schemaVersion != expectedSchema)
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

    private static bool MatchesAudio(
        string json,
        IReadOnlyList<string> normalizedIds,
        float musicVolume,
        float effectsVolume)
    {
        if (!MatchesDiscoveries(
                json,
                AudioSchemaVersion,
                normalizedIds))
        {
            return false;
        }

        PlayerProgressData data =
            JsonUtility.FromJson<PlayerProgressData>(json);

        return data != null &&
            VolumesMatch(data.musicVolume, musicVolume) &&
            VolumesMatch(data.effectsVolume, effectsVolume);
    }

    private static bool MatchesCurrent(
        string json,
        IReadOnlyList<string> normalizedIds,
        float musicVolume,
        float effectsVolume,
        CampaignSnapshot campaign)
    {
        if (!MatchesDiscoveries(
                json,
                SupportedSchemaVersion,
                normalizedIds))
        {
            return false;
        }

        PlayerProgressData data =
            JsonUtility.FromJson<PlayerProgressData>(json);

        return data != null &&
            VolumesMatch(data.musicVolume, musicVolume) &&
            VolumesMatch(data.effectsVolume, effectsVolume) &&
            data.campaignStarted == campaign.Started &&
            SameIds(data.completedMaps, campaign.CompletedMaps) &&
            SameIds(data.clearedTutorials, campaign.ClearedTutorials) &&
            SameIds(data.evaluations, campaign.Evaluations);
    }

    private static bool SameIds(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        List<string> normalizedLeft = NormalizeIds(left);
        List<string> normalizedRight = NormalizeIds(right);

        if (normalizedLeft.Count != normalizedRight.Count)
        {
            return false;
        }

        for (int index = 0; index < normalizedLeft.Count; index++)
        {
            if (!string.Equals(
                    normalizedLeft[index],
                    normalizedRight[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VolumesMatch(float left, float right)
    {
        return Math.Abs(left - right) <= 0.0001f;
    }

    private struct ParsedDocument
    {
        public bool Legacy;
        public bool AudioSchema;
        public List<string> Ids;
        public float MusicVolume;
        public float EffectsVolume;
        public CampaignSnapshot Campaign;
    }

    private static ParsedDocument ReadDocument(string json)
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

        SchemaClass schemaClass = ClassifySchema(root.SchemaRaw);

        if (schemaClass == SchemaClass.Unsupported)
        {
            throw new ProgressParseException(
                ProgressFileProblem.UnsupportedSchema,
                "schemaVersion is newer than supported version " +
                SupportedSchemaVersion + ".");
        }

        if (schemaClass == SchemaClass.Invalid)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "schemaVersion is not a supported version.");
        }

        if (root.IdsRaw == null)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "discoveredIds is missing.");
        }

        ParsedDocument document = new ParsedDocument
        {
            Legacy = schemaClass == SchemaClass.Legacy,
            Ids = ReadIds(root.IdsRaw),
            MusicVolume = DefaultMusicVolume,
            EffectsVolume = DefaultEffectsVolume
        };

        if (schemaClass == SchemaClass.Legacy)
        {
            return document;
        }

        if (root.MusicRaw == null || root.EffectsRaw == null)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "schema 2 requires musicVolume and effectsVolume.");
        }

        document.MusicVolume = ReadUnitVolume(
            root.MusicRaw,
            "musicVolume");
        document.EffectsVolume = ReadUnitVolume(
            root.EffectsRaw,
            "effectsVolume");

        if (schemaClass == SchemaClass.Audio)
        {
            document.AudioSchema = true;
            document.Campaign = CampaignSnapshot.Empty;
            return document;
        }

        if (root.StartedRaw == null ||
            root.MapsRaw == null ||
            root.TutorialsRaw == null ||
            root.EvaluationsRaw == null)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                "schema 3 requires the campaign fields.");
        }

        document.Campaign = new CampaignSnapshot(
            ReadBool(root.StartedRaw, "campaignStarted"),
            ReadIds(root.MapsRaw, "completedMaps"),
            ReadIds(root.TutorialsRaw, "clearedTutorials"),
            ReadIds(root.EvaluationsRaw, "evaluations"));
        return document;
    }

    private static string AssignOnce(
        string current,
        string rawValue,
        string fieldName)
    {
        if (current != null)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                fieldName + " is duplicated.");
        }

        return rawValue;
    }

    private static bool ReadBool(string rawValue, string fieldName)
    {
        string token = rawValue.Trim();

        if (token == "true")
        {
            return true;
        }

        if (token == "false")
        {
            return false;
        }

        throw new ProgressParseException(
            ProgressFileProblem.InvalidStructure,
            fieldName + " must be true or false.");
    }

    private static float ReadUnitVolume(
        string rawValue,
        string fieldName)
    {
        JsonText text = new JsonText(rawValue);
        text.SkipWhitespace();

        if (!text.TryReadNumber(out string token))
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                fieldName + " must be a number from 0 to 1.");
        }

        text.SkipWhitespace();

        if (!text.End)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidJson,
                fieldName + " has trailing content.");
        }

        if (!double.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number) ||
            double.IsNaN(number) ||
            double.IsInfinity(number) ||
            number < 0d ||
            number > 1d)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                fieldName + " must be a number from 0 to 1.");
        }

        return (float)number;
    }

    private enum SchemaClass
    {
        Current,
        Audio,
        Legacy,
        Unsupported,
        Invalid
    }

    private static SchemaClass ClassifySchema(
        string rawValue)
    {
        JsonText text = new JsonText(rawValue);
        text.SkipWhitespace();

        if (!text.TryReadNumber(out string token))
        {
            return SchemaClass.Invalid;
        }

        text.SkipWhitespace();

        if (!text.End)
        {
            return SchemaClass.Invalid;
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
                return SchemaClass.Current;
            }

            if (integer == AudioSchemaVersion)
            {
                return SchemaClass.Audio;
            }

            if (integer == LegacySchemaVersion)
            {
                return SchemaClass.Legacy;
            }

            if (integer > SupportedSchemaVersion)
            {
                return SchemaClass.Unsupported;
            }

            return SchemaClass.Invalid;
        }

        if (double.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double number) &&
            number > SupportedSchemaVersion)
        {
            return SchemaClass.Unsupported;
        }

        return SchemaClass.Invalid;
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

    private static List<string> ReadIds(string rawValue, string fieldName)
    {
        try
        {
            return ReadIds(rawValue);
        }
        catch (ProgressParseException)
        {
            throw new ProgressParseException(
                ProgressFileProblem.InvalidStructure,
                fieldName + " must be a list of strings.");
        }
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
        public string MusicRaw;
        public string EffectsRaw;
        public string StartedRaw;
        public string MapsRaw;
        public string TutorialsRaw;
        public string EvaluationsRaw;
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
                else if (key == "musicVolume")
                {
                    if (parsed.MusicRaw != null)
                    {
                        throw new ProgressParseException(
                            ProgressFileProblem.InvalidStructure,
                            "musicVolume is duplicated.");
                    }

                    parsed.MusicRaw = rawValue;
                }
                else if (key == "effectsVolume")
                {
                    if (parsed.EffectsRaw != null)
                    {
                        throw new ProgressParseException(
                            ProgressFileProblem.InvalidStructure,
                            "effectsVolume is duplicated.");
                    }

                    parsed.EffectsRaw = rawValue;
                }
                else if (key == "campaignStarted")
                {
                    parsed.StartedRaw = AssignOnce(
                        parsed.StartedRaw,
                        rawValue,
                        "campaignStarted");
                }
                else if (key == "completedMaps")
                {
                    parsed.MapsRaw = AssignOnce(
                        parsed.MapsRaw,
                        rawValue,
                        "completedMaps");
                }
                else if (key == "clearedTutorials")
                {
                    parsed.TutorialsRaw = AssignOnce(
                        parsed.TutorialsRaw,
                        rawValue,
                        "clearedTutorials");
                }
                else if (key == "evaluations")
                {
                    parsed.EvaluationsRaw = AssignOnce(
                        parsed.EvaluationsRaw,
                        rawValue,
                        "evaluations");
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
