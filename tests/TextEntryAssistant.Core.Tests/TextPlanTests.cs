using TextEntryAssistant.Core;

namespace TextEntryAssistant.Core.Tests;

public sealed class TextPlanTests
{
    public static Task NormalizesAllLineEndingsAndKeepsEmojiAsOneTextElement()
    {
        var units = TextPlan.Create("甲\r\n乙\r丙\n😀");

        TestAssert.SequenceEqual(
            [InputUnitKind.Text, InputUnitKind.Newline, InputUnitKind.Text, InputUnitKind.Newline, InputUnitKind.Text, InputUnitKind.Newline, InputUnitKind.Text],
            units.Select(unit => unit.Kind));
        TestAssert.Equal("😀", units[^1].Value);
        return Task.CompletedTask;
    }

    public static Task KeepsZwjEmojiSequenceTogether()
    {
        var units = TextPlan.Create("👩‍💻");

        var unit = TestAssert.Single(units);
        TestAssert.Equal(InputUnitKind.Text, unit.Kind);
        TestAssert.Equal("👩‍💻", unit.Value);
        return Task.CompletedTask;
    }
}
