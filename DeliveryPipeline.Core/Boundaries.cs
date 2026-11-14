using System.Text.Json;

namespace DeliveryPipeline.Core;

// Mutable file operations are isolated from the pure business workflow.
public sealed class JsonShipmentSource
{
    public sealed class OrderRow
    {
        public OrderRow() { }
        public string? OrderId { get; set; }
        public string? PackageCode { get; set; }
        public string? ServiceCode { get; set; }
        public string? DiscountCode { get; set; } // optional: absence means NONE (0 discount)
        public int? Quantity { get; set; }
        public decimal? DistanceKm { get; set; }
        public string? Status { get; set; }
    }

    public Result<IReadOnlyList<ShipmentRequest>> Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result<IReadOnlyList<ShipmentRequest>>.Reject(
                "FILE_PATH_REQUIRED", "A JSON input file path is required.");

        try
        {
            var json = File.ReadAllText(path);
            var rows = JsonSerializer.Deserialize<List<OrderRow?>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rows is null || rows.Count == 0)
                return BadFile(path, "Expected a non-empty JSON array of orders.");

            var parsed = new List<ShipmentRequest>();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row is null || row.OrderId is null || row.PackageCode is null ||
                    row.ServiceCode is null || row.Quantity is null ||
                    row.DistanceKm is null || row.Status is null)
                    return BadFile(path, $"Row {i + 1}: required fields are missing.");

                // An unknown package is a domain lookup failure, not a JSON parse error.
                parsed.Add(new ShipmentRequest(row.OrderId, row.PackageCode,
                    row.ServiceCode, row.DiscountCode ?? "NONE", row.Quantity.Value,
                    row.DistanceKm.Value, row.Status));
            }
            return Result<IReadOnlyList<ShipmentRequest>>.Succeed(parsed);
        }
        catch (FileNotFoundException)
        {
            return Result<IReadOnlyList<ShipmentRequest>>.Reject("FILE_NOT_FOUND",
                $"Could not load orders: file '{path}' does not exist.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result<IReadOnlyList<ShipmentRequest>>.Reject("FILE_NOT_FOUND",
                $"Could not load orders: folder for '{path}' does not exist.");
        }
        catch (JsonException ex)
        {
            return BadFile(path, $"Invalid JSON: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<IReadOnlyList<ShipmentRequest>>.Reject("FILE_ACCESS_DENIED",
                $"Could not read '{path}': {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyList<ShipmentRequest>>.Reject("FILE_IO_ERROR",
                $"Could not read '{path}': {ex.Message}");
        }
    }

    private static Result<IReadOnlyList<ShipmentRequest>> BadFile(string path, string reason) =>
        Result<IReadOnlyList<ShipmentRequest>>.Reject("MALFORMED_FILE",
            $"File '{path}' is malformed. {reason}");
}

public sealed class QuoteStore
{
    private IReadOnlyList<QuoteOutput> _quotes = Array.Empty<QuoteOutput>();
    public IReadOnlyList<QuoteOutput> Current => _quotes;

    // Atomic replacement: previous complete snapshot survives any failure.
    public void ReplaceAll(IReadOnlyList<QuoteOutput> quotes) =>
        _quotes = quotes.ToArray();
}

public sealed class QuoteApplication
{
    private readonly JsonShipmentSource _source;
    private readonly BatchProcessor _processor;
    private readonly QuoteStore _store;

    public QuoteApplication(JsonShipmentSource source, BatchProcessor processor, QuoteStore store)
    {
        _source = source;
        _processor = processor;
        _store = store;
    }

    public Result<IReadOnlyList<QuoteOutput>> ImportAndQuote(string path)
    {
        // Plan everything first; commit only after loading AND ALL domain operations succeed.
        var proposed = _source.Load(path).Bind(_processor.Prepare);
        if (proposed is Result<IReadOnlyList<QuoteOutput>>.Success ok)
            _store.ReplaceAll(ok.Value);
        return proposed;
    }
}
