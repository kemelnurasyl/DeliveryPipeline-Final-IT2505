
namespace DeliveryPipeline.Core;

// An order can only reach these states through the workflow.
public sealed record QuotedOrder
{
    public ShipmentRequest Request { get; }
    public QuoteOutput Quote { get; }

    internal QuotedOrder(ShipmentRequest request, QuoteOutput quote)
    {
        Request = request;
        Quote = quote;
    }
}

public sealed record ConfirmedOrder
{
    public ShipmentRequest Request { get; }
    public QuoteOutput Quote { get; }

    internal ConfirmedOrder(ShipmentRequest request, QuoteOutput quote)
    {
        Request = request;
        Quote = quote;
    }
}

public sealed record DispatchedOrder
{
    public string OrderId { get; }
    public string CourierId { get; }

    internal DispatchedOrder(string orderId, string courierId)
    {
        OrderId = orderId;
        CourierId = courierId;
    }
}

public static class OrderLifecycle
{
    // Step 1: Calculate and validate the delivery quote.
    public static Result<QuotedOrder> Prepare(
        QuoteService service, ShipmentRequest request) =>
        service.CreateQuote(request)
            .Map(quote => new QuotedOrder(request, quote));

    // Step 2: Customer must accept the quote.
    public static Result<ConfirmedOrder> Confirm(
        QuotedOrder order, bool customerAccepted) =>
        customerAccepted
            ? Result<ConfirmedOrder>.Succeed(
                new ConfirmedOrder(order.Request, order.Quote))
            : Result<ConfirmedOrder>.Reject(
                "CUSTOMER_DECLINED",
                "The customer did not accept the delivery quote.");

    // Step 3: Only a confirmed order can be dispatched.
    public static Result<DispatchedOrder> Dispatch(
        ConfirmedOrder order, string courierId)
    {
        if (string.IsNullOrWhiteSpace(courierId))
            return Result<DispatchedOrder>.Reject(
                "COURIER_REQUIRED",
                "A courier must be assigned before dispatch.");

        return Result<DispatchedOrder>.Succeed(
            new DispatchedOrder(order.Request.OrderId, courierId));
    }
}
