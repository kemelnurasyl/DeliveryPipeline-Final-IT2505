namespace DeliveryPipeline.Core;

public static class LazyQuoteFilters
{
    // Enumerating consumes an IN-MEMORY quote snapshot; it does not keep a file open.
    // The optional probe is used only by tests to observe when enumeration happens.
    public static IEnumerable<QuoteOutput> AtLeast(
        IEnumerable<QuoteOutput> quotes, decimal minimumKzt,
        Action? onExamined = null)
    {
        foreach (var quote in quotes)
        {
            onExamined?.Invoke();
            if (quote.Total >= minimumKzt)
                yield return quote;
        }
    }
}
