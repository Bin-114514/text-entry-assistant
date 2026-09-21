using System.Globalization;

namespace TextEntryAssistant.Core;

public static class TextPlan
{
    public static IReadOnlyList<InputUnit> Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Normalize line endings before grapheme enumeration so CRLF is one
        // deliberate newline boundary and never two independent key events.
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var units = new List<InputUnit>();
        var enumerator = StringInfo.GetTextElementEnumerator(normalized);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var kind = element switch
            {
                "\n" => InputUnitKind.Newline,
                "\t" => InputUnitKind.Tab,
                _ => InputUnitKind.Text
            };
            units.Add(new InputUnit(kind, element));
        }

        return units;
    }
}
