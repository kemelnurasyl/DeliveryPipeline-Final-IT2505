namespace DeliveryPipeline.Core;

// All report types are immutable data. They contain no file or console operations.
public sealed record PriorityQuoteRow(
    string OrderId, string ServiceCode, decimal QuoteKzt, int PriorityScore);

public sealed record ServiceRevenueRow(
    string ServiceCode, int OrderCount, decimal RevenueKzt);

public sealed record ServiceReserveRow(
    string ServiceCode, int OrderCount, decimal RevenueKzt, decimal ReserveKzt);

public sealed record ReportBundle(
    IReadOnlyList<QuoteOutput> Quotes,
    IReadOnlyList<PriorityQuoteRow> PriorityQuotes,
    IReadOnlyList<ServiceReserveRow> ServiceRevenue);
