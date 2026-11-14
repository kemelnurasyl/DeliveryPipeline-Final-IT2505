using DeliveryPipeline.Core;

// No NuGet packages needed: an assertions-based runner that reports each failure
// and returns exit code 1 if any assertion/test fails.
var tests = new (string Name, Action Run)[]
{
    ("01 Integrated STANDARD success = 7400 KZT", Tests.StandardBatch),
    ("02 Integrated EXPRESS success = 11300 KZT", Tests.ExpressBatch),
    ("03 Domain failure: invalid quantity", Tests.InvalidQuantity),
    ("04 Domain failure: missing package", Tests.MissingPackage),
    ("05 Domain failure: forbidden state", Tests.BlockedStatus),
    ("06 Domain failure: invalid distance", Tests.InvalidDistance),
    ("07 Required package lookup present", Tests.LookupPresent),
    ("08 Required package lookup absent", Tests.LookupAbsent),
    ("09 Zero discount is Some(0), not None", Tests.ValidZeroValue),
    ("10 Zero-distance shipment has valid price", Tests.ZeroDistance),
    ("11 Map success transforms payload", Tests.MapSuccess),
    ("12 Bind success keeps resulting Result", Tests.BindSuccess),
    ("13 Map failure skips dependent function", Tests.MapSkips),
    ("14 Bind failure skips dependent function", Tests.BindSkips),
    ("15 Missing file preserves committed state", Tests.MissingFilePreservesState),
    ("16 Malformed file preserves committed state", Tests.MalformedFilePreservesState),
    ("17 Domain batch failure prevents partial commit", Tests.DomainFailurePreservesState),
    ("18 Optional discount missing means NONE", Tests.OptionalDiscount),
    ("19 Empty JSON list is reported as malformed", Tests.EmptyInputFile),
    ("20 A successful Result cannot contain null", Tests.SuccessCannotContainNull),
    ("21 Custom extension: service filter", Tests.ServiceExtension),
    ("22 Custom extension: priority projection", Tests.ScoreExtension),
    ("23 Curried routine and strict produce different scores", Tests.CurriedScores),
    ("24 Priority report sorts tied quotes deterministically", Tests.PriorityTies),
    ("25 Priority report has no matches", Tests.PriorityNoMatches),
    ("26 Both reports accept empty inputs", Tests.EmptyReports),
    ("27 Service grouping and KZT totals", Tests.GroupingTotals),
    ("28 Tied service totals have stable secondary order", Tests.TiedServiceOrdering),
    ("29 yield is lazy, snapshot avoids reprocessing", Tests.DeferredMaterialisation),
    ("30 REAL native C reserve call = 36 KZT", Tests.NativeCall),
    ("31 Native validation rejects invalid amount and precision", Tests.NativeInvalid),
    ("32 Controlled adapter failure is a Result", Tests.NativeAdapterFailure),
    ("33 Full success: import + 2 reports + native + CSV", Tests.ReportWorkflowSuccess),
    ("34 Native failure preserves earlier committed state", Tests.ReportWorkflowNativeFailure),
    ("35 Export failure preserves earlier committed state", Tests.ReportWorkflowExportFailure),
    ("36 Missing input preserves earlier committed state", Tests.ReportWorkflowMissingFile),
    ("37 Malformed input preserves earlier committed state", Tests.ReportWorkflowMalformedFile),
    ("38 StreamWriter released after successful export", Tests.WriterReleased),
    ("39 In-memory yield does not retain file handles", Tests.YieldNoOpenFile),
    ("40 Report domain error does not commit or export", Tests.ReportWorkflowDomainFailure),
    ("41 Lifecycle: successful dispatch", Tests.LifecycleSuccess),
    ("42 Lifecycle: customer declined", Tests.LifecycleDeclined),
    ("43 Lifecycle: courier required", Tests.LifecycleCourierRequired),
    ("44 Lifecycle: invalid order", Tests.LifecycleInvalidOrder),
    ("45 Cancellation: correct refund", Tests.RefundValid), 
    ("46 Cancellation: boundary rates", Tests.RefundBoundaries),
    ("47 Cancellation: invalid fee", Tests.RefundInvalidRate),
    ("48 Cancellation: declined order", Tests.RefundAfterDecline)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}: {ex.GetType().Name}: {ex.Message}");
    }
}
Console.WriteLine($"RESULT: {tests.Length - failures}/{tests.Length} passed.");
return failures == 0 ? 0 : 1;

internal static class Tests
{
    private static readonly Catalog Catalog = new();
    private static QuoteService Service() => new(Catalog);
    private static ShipmentRequest Order(string id = "A", string package = "PARCEL",
        string service = "STANDARD", string discount = "NONE", int quantity = 1,
        decimal distance = 0m, string status = "DRAFT") =>
        new(id, package, service, discount, quantity, distance, status);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }

    private static T Succeeds<T>(Result<T> result) => result switch
    {
        Result<T>.Success ok => ok.Value,
        Result<T>.Failure failure => throw new InvalidOperationException(
            $"Unexpected failure: {failure.Error.Code}: {failure.Error.Message}"),
        _ => throw new InvalidOperationException("Unknown Result state")
    };

    private static void FailsWith<T>(Result<T> result, string code)
    {
        Check(result is Result<T>.Failure, $"Expected failure '{code}'.");
        var error = ((Result<T>.Failure)result).Error;
        Equal(code, error.Code);
        Check(!string.IsNullOrWhiteSpace(error.Message), "Error message must help caller.");
    }

    public static void StandardBatch()
    {
        var batch = new BatchProcessor(Service());
        var quotes = Succeeds(batch.Prepare(new[]
        { Order("A", distance: 0), Order("B", distance: 2),
          Order("C", distance: 10), Order("D", distance: 30) }));
        Equal(4, quotes.Count);
        Equal(7400m, quotes.Sum(q => q.Total));
        Equal(800m, quotes[0].Total);
    }

    public static void ExpressBatch()
    {
        var batch = new BatchProcessor(Service());
        var quotes = Succeeds(batch.Prepare(new[]
        { Order("A", service: "EXPRESS", distance: 0),
          Order("B", service: "EXPRESS", distance: 2),
          Order("C", service: "EXPRESS", distance: 10),
          Order("D", service: "EXPRESS", distance: 30) }));
        Equal(11300m, quotes.Sum(q => q.Total));
    }

    public static void InvalidQuantity() =>
        FailsWith(Service().CreateQuote(Order(quantity: 0)), "INVALID_QUANTITY");

    public static void MissingPackage() =>
        FailsWith(Service().CreateQuote(Order(package: "UNKNOWN")), "PACKAGE_NOT_FOUND");

    public static void BlockedStatus() =>
        FailsWith(Service().CreateQuote(Order(status: "DISPATCHED")), "STATE_NOT_ALLOWED");

    public static void InvalidDistance() =>
        FailsWith(Service().CreateQuote(Order(distance: -1)), "INVALID_DISTANCE");

    public static void LookupPresent()
    {
        var present = Catalog.FindPackage("PARCEL");
        Check(present is Option<PackageDefinition>.Some, "Existing package must be Some.");
        Equal("PARCEL", ((Option<PackageDefinition>.Some)present).Value.Code);
    }

    public static void LookupAbsent()
    {
        var missing = Catalog.FindPackage("UNKNOWN");
        Check(missing is Option<PackageDefinition>.None, "Unknown package must be None.");
        FailsWith(missing.Require("PACKAGE_NOT_FOUND", "Unknown package."), "PACKAGE_NOT_FOUND");
    }

    public static void ValidZeroValue()
    {
        var discount = Catalog.FindDiscount("NONE");
        Check(discount is Option<decimal>.Some, "Zero is present, not absent.");
        Equal(0m, ((Option<decimal>.Some)discount).Value);
        Check(Catalog.FindDiscount("INVALID") is Option<decimal>.None,
            "Unknown code should be absent.");
    }

    public static void ZeroDistance()
    {
        var quote = Succeeds(Service().CreateQuote(Order(distance: 0m)));
        Equal(800m, quote.Total);
    }

    public static void MapSuccess()
    {
        var output = Result<int>.Succeed(3).Map(x => x * 2);
        Equal(6, Succeeds(output));
    }

    public static void BindSuccess()
    {
        var output = Result<int>.Succeed(3).Bind(x => Result<string>.Succeed($"ID-{x}"));
        Equal("ID-3", Succeeds(output));
    }

    public static void MapSkips()
    {
        var invoked = 0;
        var failed = Result<int>.Reject("ORIGINAL", "Original error.");
        var output = failed.Map(x => { invoked++; return x * 2; });
        FailsWith(output, "ORIGINAL");
        Equal(0, invoked);
    }

    public static void BindSkips()
    {
        var invoked = 0;
        var failed = Result<int>.Reject("ORIGINAL", "Original error.");
        var output = failed.Bind(x =>
        {
            invoked++;
            return Result<string>.Succeed(x.ToString());
        });
        FailsWith(output, "ORIGINAL");
        Equal(0, invoked);
    }

    private static void InTemporaryDirectory(Action<string> action)
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "FP_Assignment7_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { action(folder); }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private const string GoodOrders = """
    [
      {"orderId":"A","packageCode":"PARCEL","serviceCode":"STANDARD","discountCode":"NONE","quantity":1,"distanceKm":0,"status":"DRAFT"},
      {"orderId":"B","packageCode":"PARCEL","serviceCode":"STANDARD","discountCode":"NONE","quantity":1,"distanceKm":2,"status":"DRAFT"}
    ]
    """;

    private static (QuoteApplication App, QuoteStore Store) NewApp()
    {
        var store = new QuoteStore();
        return (new QuoteApplication(new JsonShipmentSource(),
            new BatchProcessor(Service()), store), store);
    }

    private static void Seed(string folder, QuoteApplication app)
    {
        var validPath = Path.Combine(folder, "valid.json");
        File.WriteAllText(validPath, GoodOrders);
        var quotes = Succeeds(app.ImportAndQuote(validPath));
        Equal(2, quotes.Count);
    }

    public static void MissingFilePreservesState() => InTemporaryDirectory(folder =>
    {
        var (app, store) = NewApp();
        Seed(folder, app);
        var before = store.Current.ToArray();
        var result = app.ImportAndQuote(Path.Combine(folder, "not-there.json"));
        FailsWith(result, "FILE_NOT_FOUND");
        Equal(before.Length, store.Current.Count);
        Equal(before.Sum(x => x.Total), store.Current.Sum(x => x.Total));
    });

    public static void MalformedFilePreservesState() => InTemporaryDirectory(folder =>
    {
        var (app, store) = NewApp();
        Seed(folder, app);
        var before = store.Current.Sum(x => x.Total);
        var file = Path.Combine(folder, "broken.json");
        File.WriteAllText(file, "[{ this is not valid JSON");
        FailsWith(app.ImportAndQuote(file), "MALFORMED_FILE");
        Equal(2, store.Current.Count);
        Equal(before, store.Current.Sum(x => x.Total));
    });

    public static void DomainFailurePreservesState() => InTemporaryDirectory(folder =>
    {
        var (app, store) = NewApp();
        Seed(folder, app);
        var before = store.Current.Sum(x => x.Total);
        var file = Path.Combine(folder, "mixed.json");
        File.WriteAllText(file, """
          [
            {"orderId":"A","packageCode":"PARCEL","serviceCode":"STANDARD","quantity":1,"distanceKm":0,"status":"DRAFT"},
            {"orderId":"B","packageCode":"NO_SUCH_PACKAGE","serviceCode":"STANDARD","quantity":1,"distanceKm":2,"status":"DRAFT"}
          ]
          """);
        FailsWith(app.ImportAndQuote(file), "PACKAGE_NOT_FOUND");
        Equal(2, store.Current.Count);
        Equal(before, store.Current.Sum(x => x.Total));
    });

    public static void OptionalDiscount() => InTemporaryDirectory(folder =>
    {
        var (app, _) = NewApp();
        var file = Path.Combine(folder, "optional.json");
        File.WriteAllText(file, """
          [{"orderId":"A","packageCode":"PARCEL","serviceCode":"STANDARD","quantity":1,"distanceKm":0,"status":"DRAFT"}]
          """);
        var quotes = Succeeds(app.ImportAndQuote(file));
        Equal(800m, quotes[0].Total);
    });

    public static void EmptyInputFile() => InTemporaryDirectory(folder =>
    {
        var file = Path.Combine(folder, "empty.json");
        File.WriteAllText(file, "[]");
        FailsWith(new JsonShipmentSource().Load(file), "MALFORMED_FILE");
    });

    public static void SuccessCannotContainNull()
    {
        var threw = false;
        string? absent = null;
        try { Result<string?>.Succeed(absent); }
        catch (ArgumentNullException) { threw = true; }
        Check(threw, "Null success payload must be rejected.");
    }
    private static QuoteOutput Quote(string id, string service, decimal amount) =>
        new(id, service, amount);

    private static readonly QuoteOutput[] ReportQuotes =
    {
        Quote("A", "STANDARD", 800m),
        Quote("C", "STANDARD", 1800m),
        Quote("B", "STANDARD", 1800m),
        Quote("D", "EXPRESS", 1250m),
        Quote("F", "EXPRESS", 2750m),
        Quote("E", "EXPRESS", 2750m)
    };

    private const string ReportJson = """
    [
      {"orderId":"A","packageCode":"PARCEL","serviceCode":"STANDARD","quantity":1,"distanceKm":0,"status":"DRAFT"},
      {"orderId":"C","packageCode":"PARCEL","serviceCode":"STANDARD","quantity":1,"distanceKm":10,"status":"DRAFT"},
      {"orderId":"B","packageCode":"PARCEL","serviceCode":"STANDARD","quantity":1,"distanceKm":10,"status":"DRAFT"},
      {"orderId":"D","packageCode":"PARCEL","serviceCode":"EXPRESS","quantity":1,"distanceKm":0,"status":"DRAFT"},
      {"orderId":"F","packageCode":"PARCEL","serviceCode":"EXPRESS","quantity":1,"distanceKm":10,"status":"DRAFT"},
      {"orderId":"E","packageCode":"PARCEL","serviceCode":"EXPRESS","quantity":1,"distanceKm":10,"status":"DRAFT"}
    ]
    """;

    private sealed class FailingReserveCalculator : INativeReserveCalculator
    {
        public int Calls { get; private set; }
        public Result<decimal> CalculateReserve(decimal amountKzt, int basisPoints)
        {
            Calls++;
            return Result<decimal>.Reject("NATIVE_UNAVAILABLE", "Controlled native load failure.");
        }
    }

    private static ReportWorkflow NewWorkflow(QuoteStore? store = null,
        INativeReserveCalculator? native = null) =>
        new(new JsonShipmentSource(), new BatchProcessor(Service()),
            store ?? new QuoteStore(), native ?? new NativeReserveCalculator(),
            new ReportCsvExporter());

    private static string ReportFile(string folder)
    {
        var path = Path.Combine(folder, "input.json");
        File.WriteAllText(path, ReportJson);
        return path;
    }

    public static void ServiceExtension()
    {
        var filtered = ReportQuotes.ForService("standard").ToArray();
        Equal(3, filtered.Length);
        Check(filtered.All(q => q.ServiceCode == "STANDARD"), "Must filter service.");
    }

    public static void ScoreExtension()
    {
        var rows = new[] { Quote("X", "STANDARD", 1800m) }
            .WithPriorityScores(ScoreFactory.Routine()).ToArray();
        Equal(1, rows.Length);
        Equal("X", rows[0].OrderId);
        Equal(10, rows[0].PriorityScore);
        Equal(1800m, rows[0].QuoteKzt);
    }

    public static void CurriedScores()
    {
        var input = Quote("X", "STANDARD", 1800m);
        Func<decimal, Func<QuoteOutput, int>> curry = ScoreFactory.CreateScore;
        var routine = curry(1500m);
        var strict = curry(3000m);
        Equal(10, routine(input));
        Equal(1, strict(input));
    }

    public static void PriorityTies()
    {
        var rows = ReportQueries.PriorityQuotes(ReportQuotes,
            "STANDARD", 1500m, ScoreFactory.Routine());
        Equal(2, rows.Count);
        Equal("B", rows[0].OrderId);
        Equal("C", rows[1].OrderId);
        Equal(1800m, rows[0].QuoteKzt);
        Equal(10, rows[1].PriorityScore);
    }

    public static void PriorityNoMatches()
    {
        var rows = ReportQueries.PriorityQuotes(ReportQuotes,
            "STANDARD", 10000m, ScoreFactory.Routine());
        Equal(0, rows.Count);
    }

    public static void EmptyReports()
    {
        Equal(0, ReportQueries.PriorityQuotes(Array.Empty<QuoteOutput>(),
            "STANDARD", 0m, ScoreFactory.Routine()).Count);
        Equal(0, ReportQueries.RevenueByService(Array.Empty<QuoteOutput>()).Count);
    }

    public static void GroupingTotals()
    {
        var result = ReportQueries.RevenueByService(ReportQuotes);
        Equal(2, result.Count);
        Equal("EXPRESS", result[0].ServiceCode);
        Equal(3, result[0].OrderCount);
        Equal(6750m, result[0].RevenueKzt);
        Equal("STANDARD", result[1].ServiceCode);
        Equal(3, result[1].OrderCount);
        Equal(4400m, result[1].RevenueKzt);
    }

    public static void TiedServiceOrdering()
    {
        var rows = ReportQueries.RevenueByService(new[]
        {
            Quote("B", "STANDARD", 100m), Quote("A", "EXPRESS", 100m)
        });
        Equal("EXPRESS", rows[0].ServiceCode);
        Equal("STANDARD", rows[1].ServiceCode);
    }

    public static void DeferredMaterialisation()
    {
        var visits = 0;
        var stream = LazyQuoteFilters.AtLeast(ReportQuotes, 1800m, () => visits++);
        Equal(0, visits);  // yield body did not start
        var first = stream.ToArray();
        Equal(6, visits);  // all six source rows examined
        Equal(4, first.Length);
        Equal(4, first.ToArray().Length);
        Equal(6, visits);  // snapshot re-use did not execute yield again
        Equal(4, stream.Count());
        Equal(12, visits); // second enumeration of original stream reprocesses it
    }

    public static void NativeCall()
    {
        var native = new NativeReserveCalculator();
        Equal(36m, Succeeds(native.CalculateReserve(1800m, 200)));
        Equal(88m, Succeeds(native.CalculateReserve(4400m, 200)));
    }

    public static void NativeInvalid()
    {
        var native = new NativeReserveCalculator();
        FailsWith(native.CalculateReserve(-1m, 200), "NATIVE_INVALID_INPUT");
        FailsWith(native.CalculateReserve(1.001m, 200), "NATIVE_PRECISION");
        FailsWith(native.CalculateReserve(1m, -1), "NATIVE_INVALID_INPUT");
    }

    public static void NativeAdapterFailure()
    {
        var failed = new FailingReserveCalculator();
        FailsWith(failed.CalculateReserve(1800m, 200), "NATIVE_UNAVAILABLE");
        Equal(1, failed.Calls);
    }

    public static void ReportWorkflowSuccess() => InTemporaryDirectory(folder =>
    {
        var store = new QuoteStore();
        var output = Path.Combine(folder, "report.csv");
        var bundle = Succeeds(NewWorkflow(store).ImportAndReport(
            ReportFile(folder), output, ScoreFactory.Routine()));
        Equal(6, bundle.Quotes.Count);
        Equal(11150m, bundle.Quotes.Sum(q => q.Total));
        Equal(6, store.Current.Count);
        Equal(2, bundle.PriorityQuotes.Count);
        Equal(2, bundle.ServiceRevenue.Count);
        Equal(135m, bundle.ServiceRevenue[0].ReserveKzt);
        Equal(88m, bundle.ServiceRevenue[1].ReserveKzt);
        Check(File.Exists(output), "CSV report must be exported.");
        var csv = File.ReadAllText(output);
        Check(csv.Contains("PRIORITY"), "CSV priority section missing.");
        Check(csv.Contains("REVENUE"), "CSV revenue section missing.");
        Check(csv.Contains("135.00"), "Native value missing from export.");
    });

    public static void ReportWorkflowNativeFailure() => InTemporaryDirectory(folder =>
    {
        var store = new QuoteStore();
        store.ReplaceAll(new[] { Quote("OLD", "STANDARD", 900m) });
        var fake = new FailingReserveCalculator();
        var output = Path.Combine(folder, "report.csv");
        FailsWith(NewWorkflow(store, fake).ImportAndReport(
            ReportFile(folder), output, ScoreFactory.Routine()), "NATIVE_UNAVAILABLE");
        Equal(1, fake.Calls);
        Equal("OLD", store.Current[0].OrderId);
        Equal(1, store.Current.Count);
        Check(!File.Exists(output), "No report should be exported after native failure.");
    });

    public static void ReportWorkflowExportFailure() => InTemporaryDirectory(folder =>
    {
        var store = new QuoteStore();
        store.ReplaceAll(new[] { Quote("OLD", "STANDARD", 900m) });
        var outputIsDirectory = Path.Combine(folder, "not-a-file");
        Directory.CreateDirectory(outputIsDirectory);
        var outcome = NewWorkflow(store).ImportAndReport(
            ReportFile(folder), outputIsDirectory, ScoreFactory.Routine());
        Check(outcome is Result<ReportBundle>.Failure,
            "Destination that is a directory must fail export.");
        var error = ((Result<ReportBundle>.Failure)outcome).Error.Code;
        Check(error is "EXPORT_IO_ERROR" or "EXPORT_ACCESS_DENIED",
            "Unexpected export error: " + error);
        Equal("OLD", store.Current.Single().OrderId);
        Equal(0, Directory.GetFiles(folder, "*.tmp").Length);
    });

    public static void ReportWorkflowMissingFile() => InTemporaryDirectory(folder =>
    {
        var store = new QuoteStore();
        store.ReplaceAll(new[] { Quote("OLD", "STANDARD", 900m) });
        FailsWith(NewWorkflow(store).ImportAndReport(
            Path.Combine(folder, "absent.json"), Path.Combine(folder, "out.csv"),
            ScoreFactory.Routine()), "FILE_NOT_FOUND");
        Equal("OLD", store.Current.Single().OrderId);
    });

    public static void ReportWorkflowMalformedFile() => InTemporaryDirectory(folder =>
    {
        var store = new QuoteStore();
        store.ReplaceAll(new[] { Quote("OLD", "STANDARD", 900m) });
        var source = Path.Combine(folder, "bad.json");
        File.WriteAllText(source, "[{bad json]");
        FailsWith(NewWorkflow(store).ImportAndReport(
            source, Path.Combine(folder, "out.csv"), ScoreFactory.Routine()),
            "MALFORMED_FILE");
        Equal("OLD", store.Current.Single().OrderId);
        File.Delete(source); // The file-reading convenience API closed its handle.
        Check(!File.Exists(source), "Source file should be deletable after failure.");
    });

    public static void WriterReleased() => InTemporaryDirectory(folder =>
    {
        var output = Path.Combine(folder, "out.csv");
        Succeeds(NewWorkflow().ImportAndReport(
            ReportFile(folder), output, ScoreFactory.Routine()));
        using var exclusive = new FileStream(output, FileMode.Open, FileAccess.ReadWrite,
            FileShare.None);
        Check(exclusive.Length > 0, "Report should be completely flushed and closed.");
    });

    public static void YieldNoOpenFile() => InTemporaryDirectory(folder =>
    {
        var source = ReportFile(folder);
        var requests = Succeeds(new JsonShipmentSource().Load(source));
        var quotes = Succeeds(new BatchProcessor(Service()).Prepare(requests)).ToArray();
        var stream = LazyQuoteFilters.AtLeast(quotes, 1500m);
        File.Delete(source); // yield relies on the snapshot, not an open JSON reader.
        Equal(4, stream.ToArray().Length);
        Check(!File.Exists(source), "Snapshot enumeration should not read source again.");
    });

    public static void ReportWorkflowDomainFailure() => InTemporaryDirectory(folder =>
    {
        var store = new QuoteStore();
        store.ReplaceAll(new[] { Quote("OLD", "STANDARD", 900m) });
        var input = Path.Combine(folder, "invalid.json");
        File.WriteAllText(input, """
        [{"orderId":"Bad","packageCode":"MISSING","serviceCode":"STANDARD",
          "quantity":1,"distanceKm":10,"status":"DRAFT"}]
        """);
        var destination = Path.Combine(folder, "report.csv");
        FailsWith(NewWorkflow(store).ImportAndReport(
            input, destination, ScoreFactory.Routine()), "PACKAGE_NOT_FOUND");
        Equal("OLD", store.Current.Single().OrderId);
        Check(!File.Exists(destination), "Failed batch must not export report.");
    });

    public static void LifecycleSuccess()
    {
        var request = Order("L1", distance: 5m);

        var quoted = Succeeds(
            OrderLifecycle.Prepare(Service(), request));

        Equal(1300m, quoted.Quote.Total);

        var confirmed = Succeeds(
            OrderLifecycle.Confirm(quoted, true));

        var dispatched = Succeeds(
            OrderLifecycle.Dispatch(confirmed, "COURIER-01"));

        Equal("L1", dispatched.OrderId);
        Equal("COURIER-01", dispatched.CourierId);
    }

    public static void LifecycleDeclined()
    {
        var quoted = Succeeds(
            OrderLifecycle.Prepare(Service(), Order("L2")));

        FailsWith(
            OrderLifecycle.Confirm(quoted, false),
            "CUSTOMER_DECLINED");
    }

    public static void LifecycleCourierRequired()
    {
        var quoted = Succeeds(
            OrderLifecycle.Prepare(Service(), Order("L3")));

        var confirmed = Succeeds(
            OrderLifecycle.Confirm(quoted, true));

        FailsWith(
            OrderLifecycle.Dispatch(confirmed, ""),
            "COURIER_REQUIRED");
    }

    public static void LifecycleInvalidOrder()
    {
        FailsWith(
            OrderLifecycle.Prepare(
                Service(),
                Order("L4", quantity: -1)),
            "INVALID_QUANTITY");
    }

    public static void RefundValid()
    {
        var quoted = Succeeds(
            OrderLifecycle.Prepare(
                Service(), Order("RF1", distance: 2m)));

        var confirmed = Succeeds(
            OrderLifecycle.Confirm(quoted, true));

        var cancelled = Succeeds(
            CancellationWorkflow.Cancel(confirmed, 0.10m));

        Equal("RF1", cancelled.OrderId);
        Equal(1000m, cancelled.OriginalAmountKzt);
        Equal(100m, cancelled.CancellationFeeKzt);
        Equal(900m, cancelled.RefundKzt);
    }

    public static void RefundBoundaries()
    {
        var quoted = Succeeds(
            OrderLifecycle.Prepare(
                Service(), Order("RF2", distance: 2m)));

        var confirmed = Succeeds(
            OrderLifecycle.Confirm(quoted, true));

        var noFee = Succeeds(
            CancellationWorkflow.Cancel(confirmed, 0m));

        var fullFee = Succeeds(
            CancellationWorkflow.Cancel(confirmed, 1m));

        Equal(1000m, noFee.RefundKzt);
        Equal(0m, fullFee.RefundKzt);
    }

    public static void RefundInvalidRate()
    {
        var quoted = Succeeds(
            OrderLifecycle.Prepare(
                Service(), Order("RF3", distance: 2m)));

        var confirmed = Succeeds(
            OrderLifecycle.Confirm(quoted, true));

        FailsWith(
            CancellationWorkflow.Cancel(confirmed, -0.1m),
            "INVALID_FEE_RATE");

        FailsWith(
            CancellationWorkflow.Cancel(confirmed, 1.1m),
            "INVALID_FEE_RATE");
    }

    public static void RefundAfterDecline()
    {
        var quoted = Succeeds(
            OrderLifecycle.Prepare(
                Service(), Order("RF4", distance: 2m)));

        var result = OrderLifecycle
            .Confirm(quoted, false)
            .Bind(order =>
                CancellationWorkflow.Cancel(order, 0.10m));

        FailsWith(result, "CUSTOMER_DECLINED");
    }

}
