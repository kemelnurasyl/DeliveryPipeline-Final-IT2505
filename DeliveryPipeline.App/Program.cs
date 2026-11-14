using DeliveryPipeline.Core;


if (args.Length > 0 &&
    args[0].Equals("cancel", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 5 ||
        !decimal.TryParse(args[2],
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var cancelDistance) ||
        !bool.TryParse(args[3], out var cancelAccepted) ||
        !decimal.TryParse(args[4],
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var cancelFeeRate))
    {
        Console.WriteLine(
            "Usage: cancel <orderId> <distanceKm> <true|false> <feeRate>");
        return 2;
    }

    var cancelRequest = new ShipmentRequest(
        args[1], "PARCEL", "STANDARD", "NONE",
        1, cancelDistance, "DRAFT");

    var cancelOutcome = OrderLifecycle
        .Prepare(new QuoteService(new Catalog()), cancelRequest)
        .Bind(quoted =>
            OrderLifecycle.Confirm(quoted, cancelAccepted))
        .Bind(confirmed =>
            CancellationWorkflow.Cancel(confirmed, cancelFeeRate));

    if (cancelOutcome is Result<CancelledOrder>.Failure cancelFailure)
    {
        Console.WriteLine(
            $"FAILURE [{cancelFailure.Error.Code}]: {cancelFailure.Error.Message}");
        return 1;
    }

    var cancelled =
        ((Result<CancelledOrder>.Success)cancelOutcome).Value;

    Console.WriteLine($"SUCCESS: Order '{cancelled.OrderId}' cancelled.");
    Console.WriteLine($"Original: {cancelled.OriginalAmountKzt:0.00} KZT");
    Console.WriteLine($"Cancellation fee: {cancelled.CancellationFeeKzt:0.00} KZT");
    Console.WriteLine($"Refund: {cancelled.RefundKzt:0.00} KZT");

    return 0;
}


if (args.Length > 0 &&
    args[0].Equals("lifecycle", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 5 ||
        !decimal.TryParse(args[2],
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var distanceKm) ||
        !bool.TryParse(args[3], out var customerAccepted))
    {
        Console.WriteLine(
            "Usage: lifecycle <orderId> <distanceKm> <true|false> <courierId>");
        return 2;
    }

    var request = new ShipmentRequest(
        args[1], "PARCEL", "STANDARD", "NONE",
        1, distanceKm, "DRAFT");

 
    var lifecycleOutcome = OrderLifecycle
        .Prepare(new QuoteService(new Catalog()), request)
        .Bind(quoted =>
            OrderLifecycle.Confirm(quoted, customerAccepted))
        .Bind(confirmed =>
            OrderLifecycle.Dispatch(confirmed, args[4]));

    if (lifecycleOutcome is Result<DispatchedOrder>.Failure lifecycleFailure)
    {
        Console.WriteLine(
            $"FAILURE [{lifecycleFailure.Error.Code}]: {lifecycleFailure.Error.Message}");
        return 1;
    }

    var dispatched =
        ((Result<DispatchedOrder>.Success)lifecycleOutcome).Value;

    Console.WriteLine(
        $"SUCCESS: Order '{dispatched.OrderId}' dispatched to courier '{dispatched.CourierId}'.");
    return 0;
}

// From the solution root:
//   dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv routine
//   dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv strict
//   dotnet run --project DeliveryPipeline.App -- samples/missing-package.json reports/report.csv
var sourcePath = args.Length > 0 ? args[0] : "samples/report-orders.json";
var exportPath = args.Length > 1 ? args[1] : "reports/report.csv";
var mode = args.Length > 2 ? args[2] : "routine";
if (mode != "routine" && mode != "strict")
{
    Console.Error.WriteLine("Usage: [input.json] [output.csv] [routine|strict]");
    return 2;
}

var score = mode == "strict" ? ScoreFactory.Strict() : ScoreFactory.Routine();
var store = new QuoteStore();
var application = new ReportWorkflow(new JsonShipmentSource(),
    new BatchProcessor(new QuoteService(new Catalog())),
    store, new NativeReserveCalculator(), new ReportCsvExporter());

var outcome = application.ImportAndReport(sourcePath, exportPath, score);
if (outcome is Result<ReportBundle>.Failure failed)
{
    Console.WriteLine($"FAILURE [{failed.Error.Code}]: {failed.Error.Message}");
    Console.WriteLine($"Committed quotes unchanged: {store.Current.Count}.");
    return 1;
}
var bundle = ((Result<ReportBundle>.Success)outcome).Value;
Console.WriteLine($"SUCCESS: {bundle.Quotes.Count} quote(s) committed. Total: " +
    $"{bundle.Quotes.Sum(q => q.Total):0.00} KZT");
Console.WriteLine("REPORT 1: STANDARD priority quotes (>= 1500 KZT):");
foreach (var q in bundle.PriorityQuotes)
    Console.WriteLine($"  {q.OrderId}: {q.QuoteKzt:0.00} KZT | priority {q.PriorityScore}");
Console.WriteLine("REPORT 2: revenue and native-calculated 2% reserve by service:");
foreach (var r in bundle.ServiceRevenue)
    Console.WriteLine($"  {r.ServiceCode}: {r.OrderCount} quotes, {r.RevenueKzt:0.00} KZT, reserve {r.ReserveKzt:0.00} KZT");
Console.WriteLine($"Exported: {Path.GetFullPath(exportPath)}");
return 0;
