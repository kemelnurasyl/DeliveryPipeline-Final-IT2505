namespace DeliveryPipeline.Core;

public static class ReportExtensions
{
    // Receiver: IEnumerable<QuoteOutput>. Return: deferred IEnumerable<QuoteOutput>.
    // A business-specific case-insensitive service filter, not a replacement for LINQ Where.
    public static IEnumerable<QuoteOutput> ForService(
        this IEnumerable<QuoteOutput> quotes, string serviceCode) =>
        quotes.Where(q => string.Equals(q.ServiceCode, serviceCode,
            StringComparison.OrdinalIgnoreCase));

    // Receiver: IEnumerable<QuoteOutput>. Return: deferred projected report rows.
    // Attach a caller-configured priority score to quote data without changing quotations.
    public static IEnumerable<PriorityQuoteRow> WithPriorityScores(
        this IEnumerable<QuoteOutput> quotes, Func<QuoteOutput, int> score) =>
        quotes.Select(q => new PriorityQuoteRow(q.OrderId, q.ServiceCode,
            q.Total, score(q)));
}
