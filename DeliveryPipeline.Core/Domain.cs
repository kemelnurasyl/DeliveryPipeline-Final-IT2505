namespace DeliveryPipeline.Core;

// This delivery-pricing workflow continues the STANDARD/EXPRESS case from A5/A6.
public sealed record ShipmentRequest(
    string OrderId, string PackageCode, string ServiceCode, string DiscountCode,
    int Quantity, decimal DistanceKm, string Status);

public sealed record PackageDefinition(string Code, decimal PriceMultiplier);
public sealed record PricingPolicy(string Code, decimal BaseFee, decimal FeePerKm);
public sealed record Quote(string OrderId, string ServiceCode, decimal Total);
public sealed record QuoteOutput(string OrderId, string ServiceCode, decimal Total);

public static class ShipmentRules
{
    // Pure rule validation: rejected states are data (Result), not exceptions.
    public static Result<ShipmentRequest> Validate(ShipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
            return Result<ShipmentRequest>.Reject("ORDER_ID_REQUIRED", "Order ID is required.");
        if (request.Quantity <= 0 || request.Quantity > 1000)
            return Result<ShipmentRequest>.Reject("INVALID_QUANTITY",
                $"Order '{request.OrderId}': quantity must be from 1 to 1000 (received {request.Quantity}).");
        if (request.DistanceKm < 0 || request.DistanceKm > 10000)
            return Result<ShipmentRequest>.Reject("INVALID_DISTANCE",
                $"Order '{request.OrderId}': distance must be from 0 to 10000 km (received {request.DistanceKm}).");
        if (!string.Equals(request.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            return Result<ShipmentRequest>.Reject("STATE_NOT_ALLOWED",
                $"Order '{request.OrderId}': only DRAFT orders can be quoted (status '{request.Status}').");
        if (string.IsNullOrWhiteSpace(request.PackageCode))
            return Result<ShipmentRequest>.Reject("PACKAGE_REQUIRED",
                $"Order '{request.OrderId}': package code is required.");
        if (string.IsNullOrWhiteSpace(request.ServiceCode))
            return Result<ShipmentRequest>.Reject("SERVICE_REQUIRED",
                $"Order '{request.OrderId}': service code is required.");
        return Result<ShipmentRequest>.Succeed(request);
    }
}

public sealed class Catalog
{
    private readonly IReadOnlyDictionary<string, PackageDefinition> _packages =
        new Dictionary<string, PackageDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["PARCEL"] = new PackageDefinition("PARCEL", 1m),
            ["FRAGILE"] = new PackageDefinition("FRAGILE", 1.25m)
        };

    private readonly IReadOnlyDictionary<string, PricingPolicy> _services =
        new Dictionary<string, PricingPolicy>(StringComparer.OrdinalIgnoreCase)
        {
            ["STANDARD"] = new PricingPolicy("STANDARD", 800m, 100m),
            ["EXPRESS"] = new PricingPolicy("EXPRESS", 1250m, 150m)
        };

    // The NONE -> 0m entry demonstrates that zero is a *present* value.
    private readonly IReadOnlyDictionary<string, decimal> _discounts =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["NONE"] = 0m,
            ["STUDENT"] = 0.10m
        };

    public Option<PackageDefinition> FindPackage(string code) =>
        _packages.TryGetValue(code, out var value)
            ? Option<PackageDefinition>.Present(value)
            : Option<PackageDefinition>.Missing();

    public Option<PricingPolicy> FindService(string code) =>
        _services.TryGetValue(code, out var value)
            ? Option<PricingPolicy>.Present(value)
            : Option<PricingPolicy>.Missing();

    public Option<decimal> FindDiscount(string code) =>
        _discounts.TryGetValue(code, out var value)
            ? Option<decimal>.Present(value)
            : Option<decimal>.Missing();
}

public sealed class QuoteService
{
    private readonly Catalog _catalog;
    public QuoteService(Catalog catalog) => _catalog = catalog;

    private sealed record WithPackage(ShipmentRequest Request, PackageDefinition Package);
    private sealed record WithPolicy(ShipmentRequest Request, PackageDefinition Package, PricingPolicy Policy);
    private sealed record Ready(ShipmentRequest Request, PackageDefinition Package, PricingPolicy Policy, decimal Discount);

    // Real, active chain: validation -> required lookup -> service -> discount
    // -> potentially failing pricing -> pure display mapping.
    public Result<QuoteOutput> CreateQuote(ShipmentRequest request) =>
        ShipmentRules.Validate(request)
            .Bind(valid => _catalog.FindPackage(valid.PackageCode)
                .Require("PACKAGE_NOT_FOUND",
                    $"Order '{valid.OrderId}': package '{valid.PackageCode}' was not found.")
                .Map(package => new WithPackage(valid, package)))
            .Bind(found => _catalog.FindService(found.Request.ServiceCode)
                .Require("SERVICE_NOT_FOUND",
                    $"Order '{found.Request.OrderId}': service '{found.Request.ServiceCode}' was not found.")
                .Map(service => new WithPolicy(found.Request, found.Package, service)))
            .Bind(found => _catalog.FindDiscount(found.Request.DiscountCode)
                .Require("DISCOUNT_NOT_FOUND",
                    $"Order '{found.Request.OrderId}': discount '{found.Request.DiscountCode}' was not found.")
                .Map(discount => new Ready(found.Request, found.Package, found.Policy, discount)))
            .Bind(Calculate)
            .Map(quote => new QuoteOutput(quote.OrderId, quote.ServiceCode, quote.Total));

    private static Result<Quote> Calculate(Ready selected)
    {
        // Pure pricing formula. Bounded validated inputs prevent decimal overflow.
        var request = selected.Request;
        var gross = (selected.Policy.BaseFee + request.DistanceKm * selected.Policy.FeePerKm)
                    * request.Quantity * selected.Package.PriceMultiplier;
        var total = decimal.Round(gross * (1m - selected.Discount), 2,
            MidpointRounding.AwayFromZero);
        if (total > 1_000_000m)
            return Result<Quote>.Reject("QUOTE_LIMIT_EXCEEDED",
                $"Order '{request.OrderId}': price {total} KZT exceeds the 1000000 KZT quote limit.");
        return Result<Quote>.Succeed(new Quote(request.OrderId, selected.Policy.Code, total));
    }
}

public sealed class BatchProcessor
{
    private readonly QuoteService _service;
    public BatchProcessor(QuoteService service) => _service = service;

    public Result<IReadOnlyList<QuoteOutput>> Prepare(IReadOnlyList<ShipmentRequest> orders)
    {
        if (orders.Count == 0)
            return Result<IReadOnlyList<QuoteOutput>>.Reject("EMPTY_INPUT", "No orders to process.");

        var prepared = new List<QuoteOutput>();
        foreach (var order in orders)
        {
            var quote = _service.CreateQuote(order);
            if (quote is Result<QuoteOutput>.Failure error)
                return Result<IReadOnlyList<QuoteOutput>>.Reject(error.Error);
            if (quote is Result<QuoteOutput>.Success ok)
                prepared.Add(ok.Value);
        }
        // Returned only when *all* orders are valid: no partial batch commit.
        return Result<IReadOnlyList<QuoteOutput>>.Succeed(prepared);
    }
}
