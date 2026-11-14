using System.Globalization;
using System.Text;

namespace DeliveryPipeline.Core;

// The exporter owns its StreamWriter and closes it before the atomic file rename.
public sealed class ReportCsvExporter
{
    public Result<string> Export(string destination, ReportBundle bundle)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return Result<string>.Reject("EXPORT_PATH_REQUIRED", "Report output path is required.");

        string? temporary = null;
        try
        {
            var full = Path.GetFullPath(destination);
            var folder = Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(folder);
            temporary = Path.Combine(folder,
                "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            using (var writer = new StreamWriter(temporary, append: false, new UTF8Encoding(false)))
            {
                writer.WriteLine("section,order_id,service,quote_kzt,priority_score,order_count,reserve_kzt");
                foreach (var row in bundle.PriorityQuotes)
                    writer.WriteLine($"PRIORITY,{Csv(row.OrderId)},{Csv(row.ServiceCode)}," +
                        $"{Money(row.QuoteKzt)},{row.PriorityScore},,");
                foreach (var row in bundle.ServiceRevenue)
                    writer.WriteLine($"REVENUE,,{Csv(row.ServiceCode)}," +
                        $"{Money(row.RevenueKzt)},,{row.OrderCount},{Money(row.ReserveKzt)}");
            } // StreamWriter/FileStream disposed even when WriteLine fails.
            File.Move(temporary, full, overwrite: true);
            return Result<string>.Succeed(full);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string>.Reject("EXPORT_ACCESS_DENIED", ex.Message);
        }
        catch (IOException ex)
        {
            return Result<string>.Reject("EXPORT_IO_ERROR", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<string>.Reject("EXPORT_INVALID_PATH", ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return Result<string>.Reject("EXPORT_INVALID_PATH", ex.Message);
        }
        finally
        {
            if (temporary is not null)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { /* Preserve original failure; tempfile cleanup best-effort. */ }
                catch (UnauthorizedAccessException) { /* Preserve original failure. */ }
            }
        }
    }

    private static string Money(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Csv(string value)
    {
        // Prevent formulas from being interpreted by common spreadsheet CSV readers.
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
