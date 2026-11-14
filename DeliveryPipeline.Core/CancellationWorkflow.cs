
namespace DeliveryPipeline.Core;

public sealed record CancelledOrder(
    string OrderId,
    decimal OriginalAmountKzt,
    decimal CancellationFeeKzt,
    decimal RefundKzt
);

public static class CancellationWorkflow
{
    public static Result<CancelledOrder> Cancel(
        ConfirmedOrder order,
        decimal feeRate)
    {
        if (order is null)
            return Result<CancelledOrder>.Reject(
                "ORDER_REQUIRED",
                "A confirmed order is required.");

        if (feeRate < 0m || feeRate > 1m)
            return Result<CancelledOrder>.Reject(
                "INVALID_FEE_RATE",
                "Cancellation fee rate must be between 0 and 1.");

        decimal original = order.Quote.Total;

        decimal fee = decimal.Round(
            original * feeRate,
            2,
            MidpointRounding.AwayFromZero);

        decimal refund = original - fee;

        return Result<CancelledOrder>.Succeed(
            new CancelledOrder(
                order.Request.OrderId,
                original,
                fee,
                refund));
    }
}
