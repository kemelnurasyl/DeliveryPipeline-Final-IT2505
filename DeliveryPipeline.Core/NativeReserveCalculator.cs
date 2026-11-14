using System.Runtime.InteropServices;

namespace DeliveryPipeline.Core;

// Interface allows a controlled failure test WITHOUT replacing the real P/Invoke test.
public interface INativeReserveCalculator
{
    Result<decimal> CalculateReserve(decimal amountKzt, int rateBasisPoints);
}

internal static class NativeBindings
{
    // Linux: libdeliveryfees.so. Windows: deliveryfees.dll. Calling convention: cdecl.
    [DllImport("deliveryfees", EntryPoint = "delivery_reserve_minor",
        CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern long DeliveryReserveMinor(long amountTiyn, int rateBasisPoints);
}

public sealed class NativeReserveCalculator : INativeReserveCalculator
{
    public Result<decimal> CalculateReserve(decimal amountKzt, int rateBasisPoints)
    {
        if (amountKzt < 0m || amountKzt > 1_000_000m ||
            rateBasisPoints < 0 || rateBasisPoints > 1000)
            return Result<decimal>.Reject("NATIVE_INVALID_INPUT",
                "Reserve requires 0..1,000,000 KZT and rate 0..1000 basis points.");
        var tiyn = amountKzt * 100m;
        if (tiyn != decimal.Truncate(tiyn))
            return Result<decimal>.Reject("NATIVE_PRECISION",
                "Amount must be an exact number of tiyn (two decimal places).");

        try
        {
            var nativeResult = NativeBindings.DeliveryReserveMinor(
                checked((long)tiyn), rateBasisPoints);
            if (nativeResult < 0)
                return Result<decimal>.Reject("NATIVE_CALCULATION_ERROR",
                    "Native library rejected the reserve inputs.");
            return Result<decimal>.Succeed(nativeResult / 100m);
        }
        catch (DllNotFoundException ex)
        {
            return Result<decimal>.Reject("NATIVE_LIBRARY_NOT_FOUND", ex.Message);
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<decimal>.Reject("NATIVE_ENTRYPOINT_NOT_FOUND", ex.Message);
        }
        catch (BadImageFormatException ex)
        {
            return Result<decimal>.Reject("NATIVE_ARCHITECTURE_MISMATCH", ex.Message);
        }
    }
}
