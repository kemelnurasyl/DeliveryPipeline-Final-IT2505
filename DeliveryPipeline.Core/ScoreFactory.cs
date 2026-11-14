namespace DeliveryPipeline.Core;

public static class ScoreFactory
{
    // Curried: decimal threshold -> (QuoteOutput -> integer score).
    // Partial application makes two strategies that work on the SAME quote data.
    public static Func<QuoteOutput, int> CreateScore(decimal thresholdKzt) =>
        quote => quote.Total >= thresholdKzt ? 10 : 1;

    public static Func<QuoteOutput, int> Routine() => CreateScore(1_500m);
    public static Func<QuoteOutput, int> Strict() => CreateScore(3_000m);
}
