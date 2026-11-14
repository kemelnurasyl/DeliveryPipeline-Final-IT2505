namespace DeliveryPipeline.Core;

// A8 builds ON the A7 loader, pure QuoteService/BatchProcessor and QuoteStore.
// Plan ALL results (including native fees) and export before committing the new snapshot.
public sealed class ReportWorkflow
{
    private readonly JsonShipmentSource _source;
    private readonly BatchProcessor _processor;
    private readonly QuoteStore _store;
    private readonly INativeReserveCalculator _native;
    private readonly ReportCsvExporter _exporter;

    public ReportWorkflow(JsonShipmentSource source, BatchProcessor processor,
        QuoteStore store, INativeReserveCalculator native, ReportCsvExporter exporter)
    {
        _source = source;
        _processor = processor;
        _store = store;
        _native = native;
        _exporter = exporter;
    }

    public Result<ReportBundle> ImportAndReport(
        string inputPath, string exportPath, Func<QuoteOutput, int> score,
        string serviceCode = "STANDARD", decimal minimumKzt = 1500m,
        int reserveRateBasisPoints = 200)
    {
        if (score is null || string.IsNullOrWhiteSpace(serviceCode) || minimumKzt < 0m)
            return Result<ReportBundle>.Reject("REPORT_INVALID_CONFIG",
                "A non-null score function, service and non-negative minimum are required.");

        var readyQuotes = _source.Load(inputPath).Bind(_processor.Prepare);
        if (readyQuotes is Result<IReadOnlyList<QuoteOutput>>.Failure error)
            return Result<ReportBundle>.Reject(error.Error);
        var quotes = ((Result<IReadOnlyList<QuoteOutput>>.Success)readyQuotes).Value;

        // These functions only read the materialised A7 batch; no file is open.
        var priority = ReportQueries.PriorityQuotes(quotes, serviceCode, minimumKzt, score);
        var serviceRevenue = ReportQueries.RevenueByService(quotes);
        var reserveRows = new List<ServiceReserveRow>();
        foreach (var row in serviceRevenue)
        {
            // Real native boundary is invoked once per service group.
            var reserve = _native.CalculateReserve(row.RevenueKzt, reserveRateBasisPoints);
            if (reserve is Result<decimal>.Failure failed)
                return Result<ReportBundle>.Reject(failed.Error);
            reserveRows.Add(new ServiceReserveRow(row.ServiceCode, row.OrderCount,
                row.RevenueKzt, ((Result<decimal>.Success)reserve).Value));
        }
        var bundle = new ReportBundle(quotes, priority, reserveRows.ToArray());
        var export = _exporter.Export(exportPath, bundle);
        if (export is Result<string>.Failure exportFailed)
            return Result<ReportBundle>.Reject(exportFailed.Error);

        _store.ReplaceAll(quotes);  // Only after complete calculation and successful export.
        return Result<ReportBundle>.Succeed(bundle);
    }
}
