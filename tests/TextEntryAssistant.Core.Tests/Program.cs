namespace TextEntryAssistant.Core.Tests;

public static class Program
{
    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            (nameof(TextPlanTests.NormalizesAllLineEndingsAndKeepsEmojiAsOneTextElement), TextPlanTests.NormalizesAllLineEndingsAndKeepsEmojiAsOneTextElement),
            (nameof(TextPlanTests.KeepsZwjEmojiSequenceTogether), TextPlanTests.KeepsZwjEmojiSequenceTogether),
            (nameof(InputSessionTests.SendsInOrderAndCompletes), InputSessionTests.SendsInOrderAndCompletes),
            (nameof(InputSessionTests.RejectsDuplicateStartAndCancelsFutureBatches), InputSessionTests.RejectsDuplicateStartAndCancelsFutureBatches),
            (nameof(InputSessionTests.StopsWhenTargetChangesBeforeNextUnit), InputSessionTests.StopsWhenTargetChangesBeforeNextUnit),
            (nameof(InputSessionTests.PausesOnNewlineUntilUserChoosesShiftEnter), InputSessionTests.PausesOnNewlineUntilUserChoosesShiftEnter),
            (nameof(InputSessionTests.PropagatesPartialSendFailure), InputSessionTests.PropagatesPartialSendFailure)
        };
        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
            }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }
}

internal static class TestAssert
{
    public static void True(bool condition, string message = "expected true") { if (!condition) throw new InvalidOperationException(message); }
    public static void False(bool condition, string message = "expected false") { if (condition) throw new InvalidOperationException(message); }
    public static void Equal<T>(T expected, T actual, string? message = null) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message ?? $"expected {expected}, got {actual}");
    }
    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual)) throw new InvalidOperationException($"sequences differ: [{string.Join(",", expected)}] vs [{string.Join(",", actual)}]");
    }
    public static T Single<T>(IEnumerable<T> values)
    {
        var list = values.ToList();
        if (list.Count != 1) throw new InvalidOperationException($"expected one item, got {list.Count}");
        return list[0];
    }
    public static void Empty<T>(IEnumerable<T> values) { if (values.Any()) throw new InvalidOperationException("expected empty sequence"); }
    public static void Contains(string expected, string? actual) { if (actual?.Contains(expected, StringComparison.Ordinal) != true) throw new InvalidOperationException($"'{expected}' not found"); }
    public static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"expected {typeof(T).Name}");
    }
}
