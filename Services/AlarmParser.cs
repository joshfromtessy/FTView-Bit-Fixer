using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FT_AlarmFixer.Models;

namespace FT_AlarmFixer.Services;

public sealed class AlarmParser
{
    private static readonly Regex LeadingBracketRegex = new(@"^\s*\[[^\]]*\]\s*", RegexOptions.Compiled);
    private static readonly Regex BitSuffixRegex = new(@"\.(\d+)$", RegexOptions.Compiled);

    public IReadOnlyList<AlarmRow> ParseFile(string path)
    {
        var doc = XDocument.Load(path);
        var triggerLookup = doc
            .Descendants()
            .Where(e => e.Name.LocalName == "trigger")
            .Select(e => new TriggerInfo(
                e.Attribute("id")?.Value ?? string.Empty,
                e.Attribute("type")?.Value ?? string.Empty,
                e.Attribute("exp")?.Value ?? string.Empty))
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

        var rows = new List<AlarmRow>();

        foreach (var message in doc.Descendants().Where(e => e.Name.LocalName == "message"))
        {
            var triggerRef = message.Attribute("trigger")?.Value ?? string.Empty;
            var triggerId = triggerRef.TrimStart('#');
            triggerLookup.TryGetValue(triggerId, out var trigger);

            var triggerValueRaw = message.Attribute("trigger-value")?.Value;
            int? triggerValue = TryParseInt(triggerValueRaw);

            var tag = BuildTag(trigger, triggerValue);
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var text = message.Attribute("text")?.Value ?? string.Empty;
            var description = LeadingBracketRegex.Replace(text, string.Empty).Trim();

            rows.Add(new AlarmRow(tag, description));
        }

        return rows;
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string BuildTag(TriggerInfo? trigger, int? triggerValue)
    {
        if (trigger is null)
        {
            return string.Empty;
        }

        var baseTag = ExtractTagName(trigger.Expression);
        if (string.IsNullOrWhiteSpace(baseTag))
        {
            return string.Empty;
        }

        if (trigger.IsBit && triggerValue.HasValue)
        {
            var withoutBit = RemoveBitSuffix(baseTag);
            var bitIndex = Math.Max(0, triggerValue.Value - 1);
            return $"{withoutBit}.{bitIndex}";
        }

        return CorrectBitSuffix(baseTag);
    }

    private static string ExtractTagName(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var trimmed = expression.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var endBracket = trimmed.IndexOf(']');
            if (endBracket >= 0 && endBracket + 1 < trimmed.Length)
            {
                trimmed = trimmed[(endBracket + 1)..];
            }
        }

        return trimmed.Trim();
    }

    private static string CorrectBitSuffix(string tag)
    {
        return BitSuffixRegex.Replace(tag, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bit))
            {
                return match.Value;
            }

            if (bit <= 0)
            {
                return ".0";
            }

            return "." + (bit - 1).ToString(CultureInfo.InvariantCulture);
        });
    }

    private static string RemoveBitSuffix(string tag)
    {
        return BitSuffixRegex.Replace(tag, string.Empty);
    }

    private sealed record TriggerInfo(string Id, string Type, string Expression)
    {
        public bool IsBit => string.Equals(Type, "bit", StringComparison.OrdinalIgnoreCase);
    }
}
