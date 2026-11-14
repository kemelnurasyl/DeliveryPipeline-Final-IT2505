# Assignment 8: Extension Methods and LINQ

**Student / file name:** Kemel Nurassyl (archive naming as requested)  
**Group:** IT-2505  
**Team:** Individual (1 student)  
**Project:** DeliveryPipeline: STANDARD/EXPRESS delivery quotations, extended from the uploaded Assignment 7 source.

> **IMPORTANT BEFORE SUBMISSION:** The code in this archive includes an Assignment 8 extension prepared with coding assistance. Your course requires you to explain and defend **every section you submit** and warns that unexplained copied/AI-generated code receives zero. Review, run, fix if needed, and make the implementation your own before submission. No .NET test pass is claimed here because the construction environment did not have the .NET SDK.

## 1. Continuation from Assignment 7

The original Assignment 7 files were copied as a baseline, keeping the same `DeliveryPipeline` solution/project names and existing application domain:

- `DeliveryPipeline.Core/Domain.cs`: `ShipmentRules`, `Catalog`, `QuoteService`, and `BatchProcessor` for STANDARD/EXPRESS delivery quotes; validation and calculations are pure. Package multipliers and discounts are unchanged.
- `DeliveryPipeline.Core/Outcomes.cs`: `Result<T>`, `Option<T>`, and the original `Map`, `Bind`, and `Require` extension methods (preserved).
- `DeliveryPipeline.Core/Boundaries.cs`: `JsonShipmentSource`, `QuoteApplication`, and `QuoteStore`, including all-or-nothing commits (preserved).
- `DeliveryPipeline.Tests/Program.cs`: original 20 tests (preserved), plus 20 Assignment 8 tests.
- `samples/orders.json`, `samples/express-orders.json`, and the other A7 scenario files (preserved).

The A7 console entry point has been **updated**, not split into a separate standalone app: it now calls a new `ReportWorkflow` using the **same** A7 loader, quote rules, processing and store. The old A7 pipeline remains directly used by the new workflow.

## 2. Prerequisites / reproducible commands

**Supported binary architectures:** Linux x86-64 (`.so`) and Windows x64 (`.dll`); both native libraries were compiled, but C# execution on these platforms still needs local verification.  
**Native toolchains used:** Linux x64: GCC 14.2.0, C11, `-O2 -fPIC -shared`. Windows x64 DLL cross-build: `clang-cl` 17 and `lld-link` (no runtime imports); Windows rebuild alternative: MinGW-w64 GCC.  
**C# target:** .NET 8.0 (`net8.0`, C# 12), SDK 8.0 or newer. The .NET build/test has not been executed here.  
**Working directory:** directory containing `DeliveryPipeline.sln` (the root of this extracted project).  
**NuGet dependencies:** none (the project uses its original assertion-based console test runner).

Linux x64 (from the project root):

```bash
chmod +x scripts/*.sh
./scripts/build-native.sh
dotnet build DeliveryPipeline.sln
dotnet run --project DeliveryPipeline.Tests
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv routine
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/strict-report.csv strict
```

Or simply run `./scripts/run-tests.sh` / `./scripts/run-demo.sh` after installing .NET 8.

Windows x64 (PowerShell): the precompiled `native/runtime/win-x64/deliveryfees.dll` is **included**, so only .NET 8 SDK is needed to run. Use the following commands with the bundled DLL (no GCC install required):

```powershell
dotnet build DeliveryPipeline.sln
dotnet run --project DeliveryPipeline.Tests
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv routine
```

To rebuild the Windows DLL from C on Windows, install MinGW-w64 GCC (for example MSYS2 UCRT64), put `gcc` on PATH and run `.\scripts\build-native-windows.ps1`.

Both binaries are included in the ZIP: `native/runtime/linux-x64/libdeliveryfees.so` and `native/runtime/win-x64/deliveryfees.dll`. For reproducibility, rebuild Linux with `scripts/build-native.sh`, rebuild Windows on Windows with `scripts/build-native-windows.ps1` (requires GCC), or cross-build the Windows DLL on Linux with `scripts/build-native-crosswin.sh` (requires `clang-cl` and `lld-link`). The DLL is intentionally compiled without Windows CRT imports because this C function needs no runtime allocation or libraries. `DeliveryPipeline.App.csproj` and `DeliveryPipeline.Tests.csproj` use conditional rules to copy the appropriate DLL/SO automatically to the .NET output directory. If switching platforms, use `dotnet clean` and rebuild after selecting the native binary. A binary being compiled does not by itself prove that a .NET process can load it: run the test suite locally.

**Expected app success:** exit code 0. **Expected domain/file/native/export failure:** exit code 1 with `[ERROR_CODE]`.  
**Test command:** `dotnet run --project DeliveryPipeline.Tests`. Returns exit code 0 only when all assertions pass. Never use `dotnet test` as a substitute: these are console-runner tests, not an xUnit/NUnit test assembly.

## 3. Original domain rules

A7 source uses JSON arrays with fields: `orderId`, `packageCode`, `serviceCode`, optional `discountCode`, `quantity`, `distanceKm`, and `status`.

- Status must be `DRAFT`; quantities 1..1000; distance 0..10000 km.
- Package codes: `PARCEL` multiplier 1.00; `FRAGILE` multiplier 1.25.
- STANDARD: base 800 KZT, +100 KZT/km; EXPRESS: base 1250 KZT, +150 KZT/km.
- `NONE` discount = 0%; `STUDENT` = 10%. Missing discount code defaults to NONE. Unknown supplied discount fails.
- Final quote = `Round((base + distance * perKm) * quantity * packageMultiplier * (1-discount), 2, AwayFromZero)` KZT. Quote > 1,000,000 KZT is rejected.
- A full batch must validate and quote successfully before `QuoteStore` is changed; failure retains the previous snapshot.

Old examples still work through `BatchProcessor` and A7 tests: 4 STANDARD quotes = **7,400 KZT**, 4 EXPRESS quotes = **11,300 KZT**.

## 4. Assignment 8 reports (Task 2)

**Shared source for both:** the `samples/report-orders.json` input, loaded via A7 `JsonShipmentSource.Load`, then converted to `QuoteOutput` by A7 `BatchProcessor.Prepare`. Values are not invented in the reporting code.

**Report 1: High-value STANDARD priority quotes**

- **User:** delivery operations dispatcher deciding which substantial quotes deserve review.
- **Filter:** service == `STANDARD` (case-insensitive) and quote value >= **1,500 KZT**.
- **Stages:** `LazyQuoteFilters.AtLeast` (`yield return`), `ForService` (custom extension/Where), `WithPriorityScores` (custom projection/Select), `OrderByDescending(QuoteKzt)`, `ThenBy(OrderId, Ordinal)`, `ToArray`.
- **Output:** `OrderId, ServiceCode, QuoteKzt, PriorityScore`.
- **Expected results from provided report-orders.json:** `B / STANDARD / 1800 / 10`, then `C / STANDARD / 1800 / 10`. The source deliberately orders C before B, so the final B-before-C order demonstrates the deterministic tie-breaker.
- **No match:** returns an empty list and still exports the revenue report.

**Report 2: Total revenue and 2% operational reserve by service**

- **User:** operations/finance planning. Aggregated revenue is useful for allocating a small reserve by service line, not for charging customers an additional price.
- **Stages:** `GroupBy(ServiceCode)`, `Select` to group totals, `Count` rows, `Sum` KZT, `OrderByDescending(RevenueKzt)`, `ThenBy(ServiceCode, Ordinal)`, `ToArray`.
- **Output:** `ServiceCode, OrderCount, RevenueKzt`, plus `ReserveKzt` computed by native C at the boundary.
- **Expected from sample:** EXPRESS, 3 orders, **6,750.00 KZT**, native reserve **135.00 KZT**; STANDARD, 3 orders, **4,400.00 KZT**, native reserve **88.00 KZT**. Combined quotes = **11,150.00 KZT**. Reserve total = **223.00 KZT**.

These amounts illustrate an *internal resource-planning reserve*. It is not an actual accounting expense, statutory charge or customer invoice fee. No data is changed by either pure LINQ report.

The exporter writes **both reports** to one CSV `reports/report.csv`, with a `section` field of `PRIORITY` or `REVENUE`, which can be opened in Excel. The actual output format is demonstrated in `samples/expected-report.csv`.

## 5. Two new custom extension methods (Task 1)

`DeliveryPipeline.Core/ReportExtensions.cs` (both are used in the first report and tested):

1. **`ForService(this IEnumerable<QuoteOutput> quotes, string serviceCode) : IEnumerable<QuoteOutput>`** filters by case-insensitive service code. This belongs on a collection of delivery quotes because the operation is a frequently reusable query on this domain's `ServiceCode`, without modifying collection contents. It adds meaningful case-insensitive behaviour instead of merely renaming `Where`.
2. **`WithPriorityScores(this IEnumerable<QuoteOutput> quotes, Func<QuoteOutput,int> score) : IEnumerable<PriorityQuoteRow>`** attaches a configurable operational score to each quote to create presentation-independent report data. This belongs on a stream of quotes because it projects multiple domain items into report rows, with the configured function passed in instead of a global variable.

Original A7 `Map`, `Bind` and `Require` extensions in `Outcomes.cs` continue to compose the real pricing workflow and remain tested.

## 6. Currying / partial application (Task 3)

`ScoreFactory.CreateScore : decimal -> (QuoteOutput -> int)` is a curried function. It fixes a KZT threshold on its first call; the returned function receives a quote later.

```csharp
var routine = ScoreFactory.CreateScore(1500m);
var strict = ScoreFactory.CreateScore(3000m);
var sameQuote = new QuoteOutput("B", "STANDARD", 1800m);
// routine(sameQuote) == 10; strict(sameQuote) == 1
```

Scores are 10 when the quote meets the configured threshold and 1 otherwise. The console chooses `routine` or `strict` and **passes the function to `ReportWorkflow.ImportAndReport`**. No mutable static setting is changed. The score informs operational priority; it does not change quote prices.

## 7. Deferred execution and snapshots (Task 4)

`LazyQuoteFilters.AtLeast` is an iterator over **actual QuoteOutput objects** with `yield return`. Creating the enumerable does nothing. It starts examining quotes only when enumerated; an optional `onExamined` callback exists solely as a controlled test probe, not as a production logging side effect.

`Tests.DeferredMaterialisation` proves: 0 visits before enumeration, 6 after `ToArray`, 6 after reusing the array, 12 after enumerating the **original** lazy query a second time. Materialisation happens deliberately at the LINQ report's `ToArray` and when A7 `QuoteStore.ReplaceAll` makes a snapshot. Reusing the array prevents repeated pipeline calculations and stable report pages are obtained. No lazy iterator holds an open file: A7's `File.ReadAllText` completes before yielding on the in-memory snapshot.

## 8. Native C component: function contract (Tasks 7–9)

**C files:** `native/deliveryfees.c`, `.h`.  
**Export:** `int64_t delivery_reserve_minor(int64_t amount_tiyn, int32_t rate_bp)` using `stdint.h`.  
**Units:** amount and return are signed 64-bit **tiyn** (100 tiyn = 1 KZT). `rate_bp` is signed 32-bit integer basis points (100 basis points = 1%). No native pointers/allocations.  
**Valid inputs:** amount 0..100,000,000 tiyn (0..1,000,000 KZT); rate 0..1000 bp (0..10%). On invalid input return `-1`. For non-negative valid inputs, reserves are rounded to the nearest tiyn, half-up.  
**Manual calculation:** 1,800.00 KZT = 180,000 tiyn; 200 bp = 2%; 180,000 * 200 / 10,000 = **3,600 tiyn = 36.00 KZT**.

**Actual integration:** `NativeBindings.DeliveryReserveMinor` uses `DllImport("deliveryfees")`, exported name `delivery_reserve_minor`, `cdecl`, `long` (64-bit) and `int` (32-bit). `NativeReserveCalculator` checks valid KZT range and exact two-decimal precision before invocation. It maps `DllNotFoundException`, `EntryPointNotFoundException`, `BadImageFormatException` to explicit `Result<decimal>` error codes, and converts a C return of -1 to `NATIVE_CALCULATION_ERROR`. `ReportWorkflow` calls the native adapter to fill **each** `ServiceReserveRow` in the real report.

**Build scripts and binaries:** `scripts/build-native.sh` creates `native/runtime/linux-x64/libdeliveryfees.so` from C source (binary included). `scripts/build-native-windows.ps1` rebuilds `native/runtime/win-x64/deliveryfees.dll` on Windows with MinGW-w64 GCC; `scripts/build-native-crosswin.sh` cross-builds it on Linux with Clang/LLD. Both Linux and Windows x64 binaries are bundled. The App and Tests projects copy the platform-specific compiled binary into their .NET output directories. `.NET bin` and `obj` are deliberately excluded from the submitted ZIP.

**Integration difficulty and resolution:** While cross-building the Windows DLL, `lld-link` initially failed because `libcmt.lib` and `oldnames.lib` were not installed. This deterministic integer C function does not use the C runtime, so adding `/NODEFAULTLIB /NOENTRY` produced a minimal Windows DLL with no imports; its exported symbol was checked in the PE export table. The .NET resolver also needs an OS-specific name and correct output path. The source uses the common P/Invoke base name `deliveryfees`, native build scripts, and platform-conditional `.csproj` copy rules. Check actual loading on the submission machine; the Linux C export has been smoke-tested directly, but the .NET binding has not been executed in the archive creation environment.

**Tests:** test 30 is a **real** native P/Invoke value check (36.00 KZT), not a mocked result. Test 32 injects a failing calculator to verify the controlled error path, and test 34 verifies the workflow does not commit partial state or export on that failure. The failure double does **not** replace the real integration test.

## 9. Resource ownership and allocation note (Tasks 5–6)

- **JSON file reading (A7):** `JsonShipmentSource.Load` uses `File.ReadAllText`, a convenience API that internally opens, fully reads, and closes the file. It returns a parsed in-memory list. No open file handle reaches the deferred iterator. A7 and A8 malformed/missing-file tests cover failure paths and test 37 verifies deletion after malformed content.
- **CSV file writing (A8):** `ReportCsvExporter` creates and owns a `StreamWriter`. A `using` block releases it whether lines succeed or throw. Writing targets a random temporary file in the destination directory, then `File.Move(..., overwrite:true)` publishes it after the writer is disposed; the `finally` block attempts to remove temporary files on errors. Test 38 opens the finished report exclusively and tests 35–37 cover failures with unchanged committed state.
- **Managed data:** `JsonShipmentSource` materialises source rows; `BatchProcessor.Prepare` materialises quotes; report queries materialise final `ToArray` snapshots; `QuoteStore.ReplaceAll` copies to its new committed array. This costs memory proportional to the batch but avoids repeated JSON file I/O and allows reliable all-or-nothing state updates. The lazy filter captures its in-memory quote source until enumeration and its owner is released. No long-lived cache, event subscription, unmanaged handle or finaliser is used. Large datasets could favour streaming, but the current bounded, transaction-like workflow needs a stable snapshot. There is no claim of an observed memory leak.

**Failure consistency:** A8 computes quotes, both reports, and *every* native reserve before touching committed state. It exports successfully first and only then performs `QuoteStore.ReplaceAll`. Missing/malformed input, invalid domain data, native adapter failure or export failure therefore leaves the previous quote snapshot unchanged. The app returns a clear `Result.Failure`, never a partial success.

## 10. Requirement-to-file mapping

| Assignment 8 requirement | Code implementing it | Automated proof |
|---|---|---|
| 1: Two custom extensions | `ReportExtensions.ForService`, `.WithPriorityScores` | 21, 22 |
| 2: Two reports + LINQ operators | `ReportQueries.PriorityQuotes`, `.RevenueByService` | 24–28, 33 |
| 3: Currying / two configurations | `ScoreFactory.CreateScore`, CLI routine/strict | 23 |
| 4: `yield` and materialisation | `LazyQuoteFilters.AtLeast` and both report `ToArray`s | 29, 39 |
| 5: Explicit resource ownership | `Boundaries.JsonShipmentSource.Load`, `ReportCsvExporter.Export` | 15, 16, 35–39 |
| 6: Retention / allocation note | Section 9; immutable report records and snapshot storage | 29, 33 |
| 7: Native C contribution to report | `native/deliveryfees.c`, service reserve in report #2 | 30, 33 |
| 8: C# P/Invoke and adapter | `NativeReserveCalculator.cs`, scripts, .csproj copy rules | 30–34 |
| 9: Native build/test/failure/docs | `scripts/`, README, tests | 30–34 |
| Preserve A7 pipeline / failures | `Domain.cs`, `Outcomes.cs`, `Boundaries.cs`, `ReportWorkflow.cs` | 1–20, 33–40 |

## 11. All automated test cases and expected outcomes

The test runner is `DeliveryPipeline.Tests/Program.cs` with **40 assertion-based tests**. No extra test framework packages are necessary.

**Existing Assignment 7 tests (unchanged):** 1 STANDARD 7,400; 2 EXPRESS 11,300; 3 invalid quantity; 4 unknown package; 5 blocked status; 6 negative distance; 7 present Option; 8 absent Option; 9 Some(0) not None; 10 distance 0 valid; 11 Map success; 12 Bind success; 13 Map failure skips function; 14 Bind failure skips function; 15 missing file keeps snapshot; 16 malformed file keeps snapshot; 17 domain failure keeps snapshot; 18 optional discount; 19 empty input file; 20 no null Success.

**Assignment 8 additions:**

| # | Assertion expectation |
|---|---|
| 21 | Case-insensitive `ForService` selects 3 STANDARD rows |
| 22 | `WithPriorityScores` produces correct ID/amount/score |
| 23 | Partially applied routine gives 10 and strict gives 1 for same 1,800-KZT quote |
| 24 | Tied STANDARD amount sorts B before C with correct amount/score |
| 25 | High threshold produces no matching quotes |
| 26 | Both query functions return empty arrays on empty input |
| 27 | Service grouping returns EXPRESS: 3, 6,750; STANDARD: 3, 4,400 |
| 28 | Tied aggregate amounts order by EXPRESS before STANDARD |
| 29 | Lazy iterator counter 0 before, 6 after enumeration, 6 after array reuse, 12 after repeat enumeration |
| 30 | Real native call yields 36.00 KZT for 1,800 at 2%; also 88.00 for 4,400 at 2% |
| 31 | Negative values, >2 decimal places, and invalid basis points are rejected |
| 32 | Injected failing native calculator returns explicit `NATIVE_UNAVAILABLE` |
| 33 | Real complete workflow produces 6 quotes, correct totals, native reserve, CSV and committed state |
| 34 | Native failure does not change previous committed quote or export a CSV |
| 35 | Failed export keeps state and removes its temporary file |
| 36 | Missing input keeps state |
| 37 | Malformed input keeps state; the opened JSON file can be deleted |
| 38 | Exported CSV writer has closed, evidenced by exclusive FileStream opening |
| 39 | Deferred snapshot works after source JSON file is deleted |
| 40 | Domain rejection does not commit the batch or export the report |

**Latest execution status:** 40 tests authored; **not executed** by .NET in the creation environment (SDK unavailable). Native C library **was compiled for Linux x64 and Windows x64**; Linux native function values were smoke-tested and Windows PE exports were inspected. Run the automated test command and add actual `RESULT: 40/40 passed` output here only after seeing it on your own computer. Do not label unrun tests as passed.

## 12. Defense demonstrations (7 points)

1. **Run both reports:** `dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv routine`. Explain first report's filter, projection, sort and `ThenBy`, then second report's `GroupBy`, `Sum` and `Count`.
2. **Show both new extensions:** `ReportExtensions.cs`, their `this IEnumerable<QuoteOutput>` receivers, lazy outputs and actual invocation from `ReportQueries.PriorityQuotes`.
3. **Currying:** `ScoreFactory.CreateScore(1500m)` vs `(3000m)`. Show that the same 1,800-KZT quote scores 10 and 1; run with `strict` and point to the changed score.
4. **Laziness:** test 29's counter: 0 before enumeration, 6 after `ToArray`; explain extra enumeration costs and snapshot tradeoff. Show exporter `using` and file cleanup.
5. **Native:** show `native/deliveryfees.c`, `NativeReserveCalculator.cs`, compiled `.so`/DLL, real native test 30, and that the 2% reserve appears in report #2 and exported CSV.
6. **Map/Bind and skipped steps:** quote validation and lookups in original `QuoteService.CreateQuote`; tests 13 and 14 prove dependent callbacks are not invoked after failures, not merely skipped in console output.
7. **Normal/failure workflows:** show test 33 commits and 34/40 leave earlier state intact. Change the `minimumKzt` default from 1500 to 1900, rerun relevant tests (adjust expected report assertions for the threshold) and explain the resulting empty priority report. Restore threshold after defense.

## 13. Individual contribution

**Kemel Nurassyl, IT-2505, individual submission:** supplied A7 baseline covers domain validation, STANDARD/EXPRESS pricing, `Result/Option`, Map/Bind, JSON error handling, quote store consistency and the 20 original tests. Assignment 8 extension set consists of report queries, two report-specific extensions, configured scoring, lazy filtering, a native reserve function and adapter, CSV exporter, updated application workflow, new tests and documentation. **The student must personally review, implement/modify as needed and be able to explain these additions before describing them as their own contribution.** No other team member is asserted.

## 14. Submission packaging

Submit this **ZIP archive itself** on the LMS, not a link or a PDF; keep `.cs`, `.csproj`, `.sln`, `.c`, `.h`, native build scripts, samples, tests, and README. `bin/`, `obj/`, IDE files, and secrets have been excluded. Native Linux and Windows runtime binaries reside only in clearly named folders under `native/runtime/`. Keep the solution for the final project and place it in a Git repository by the course's final deadline; repository links never replace the LMS ZIP.
