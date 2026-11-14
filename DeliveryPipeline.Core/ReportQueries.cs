namespace DeliveryPipeline.Core;

public static class ReportQueries
{
    // Report #1: operational priority list for dispatch coordinators.
    // Filter -> business extension -> projection -> deterministic ordering -> snapshot.
    public static IReadOnlyList<PriorityQuoteRow> PriorityQuotes(
        IEnumerable<QuoteOutput> quotes, string serviceCode, decimal minimumKzt,
        Func<QuoteOutput, int> score) =>
        LazyQuoteFilters.AtLeast(quotes, minimumKzt)
            .ForService(serviceCode)
            .WithPriorityScores(score)
            .OrderByDescending(row => row.QuoteKzt)
            .ThenBy(row => row.OrderId, StringComparer.Ordinal)
            .ToArray();

    // Report #2: financial revenue totals for operations and planning.
    // GroupBy + Count + Sum + deterministic aggregate order.
    public static IReadOnlyList<ServiceRevenueRow> RevenueByService(
        IEnumerable<QuoteOutput> quotes) =>
        quotes.GroupBy(q => q.ServiceCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ServiceRevenueRow(
                group.Key.ToUpperInvariant(), group.Count(), group.Sum(q => q.Total)))
            .OrderByDescending(row => row.RevenueKzt)
            .ThenBy(row => row.ServiceCode, StringComparer.Ordinal)
            .ToArray();
}
