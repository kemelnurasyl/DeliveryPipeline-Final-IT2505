DeliveryPipeline Final Project
Course: Introduction to Functional Programming (IT-2505)
Student: Kemel Nurassyl
Project type: Individual (verify team details before submission)
Platform: C# / .NET 8 (console application), native C library
Repository: https://github.com/kemelnurasyl/DeliveryPipeline-Final-IT2505
Branch: main
Initial Git commit (not the final submission commit): e9aedfb
Final submission commit: Record after all reports, tests, and documentation have been completed.
1. Problem, users, and scope
DeliveryPipeline helps a small delivery operation calculate STANDARD and EXPRESS shipment quotations, process batches safely, prioritise orders, and prepare revenue reports. It also demonstrates a quote-to-dispatch path and a cancellation/refund calculation. Intended users include dispatchers, order handlers, and an operations or finance manager.

This is a console application. It does not require a database, login, cloud platform, or graphical interface.
Implemented command-line scenarios
1. Import, quote and report: load JSON orders; validate them; calculate quotes; generate high-value STANDARD priority quotes and service-level revenue/2% reserve; export both reports to CSV. An invalid batch does not replace the committed snapshot.
2. Quote, confirm and dispatch (demonstration): calculate and validate a DRAFT order, require customer acceptance, require a courier, and return a dispatched-order result. Rejection prevents later stages.
3. Quote, confirm and calculate cancellation refund (demonstration): validate an order, require acceptance, calculate a configurable cancellation fee, and return the refund. Invalid fee rates and declined confirmations fail explicitly.
Scope limitation: The lifecycle and cancellation commands work on newly constructed in-memory example requests. They do not retrieve or update persistent orders, record financial transfers, or maintain a cross-invocation order history. In particular, the lifecycle and cancellation commands should not be presented as a persistent order management system. The instructor determines whether these scenarios satisfy the required count of distinct meaningful workflows.
2. How to build, run, and test
Prerequisites
- .NET 8 SDK (dotnet --version); verified locally with SDK 8.0.425 on Windows.
- Windows x64 or Linux x64 for the provided precompiled native library.
- From the solution folder containing DeliveryPipeline.sln.
Build
dotnet build DeliveryPipeline.sln
Run the complete deterministic test suite
dotnet run --project DeliveryPipeline.Tests
DeliveryPipeline.Tests is an assertion-based console test runner, not an xUnit/NUnit assembly. The Windows test run reported 48/48 passed, with zero compile errors and warnings. This is the result provided by the project owner on 14 November 2026, not a claim that the tests ran in every environment.
Demonstrate successful import, reports and export
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/final-report.csv routine
Observed sample results: six quotes totalling 11,150.00 KZT; EXPRESS three quotes totalling 6,750.00 KZT with 135.00 KZT reserve; STANDARD three quotes totalling 4,400.00 KZT with 88.00 KZT reserve. The reserve is an internal illustrative 2% operational reserve, not a government charge or a customer fee.
Demonstrate different configured functions
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/strict-report.csv strict
For both orders B and C (1,800.00 KZT), routine yields priority score 10, while strict yields 1. These settings change the priority score, not the delivery price.
Demonstrate failure propagation and no partial commit
dotnet run --project DeliveryPipeline.App -- samples/missing-package.json reports/error-report.csv routine
Expected: FAILURE [PACKAGE_NOT_FOUND] and Committed quotes unchanged: 0 in a fresh process.
Demonstrate order lifecycle
dotnet run --project DeliveryPipeline.App -- lifecycle L1 5 true COURIER-01
dotnet run --project DeliveryPipeline.App -- lifecycle L2 5 false COURIER-01
First command returns a successful dispatch to COURIER-01. Second returns CUSTOMER_DECLINED.
Demonstrate cancellation/refund calculation
dotnet run --project DeliveryPipeline.App -- cancel L3 5 true 0.10
dotnet run --project DeliveryPipeline.App -- cancel L4 5 false 0.10
The first command returns an original quote of 1,300.00 KZT, a hypothetical 10% cancellation fee of 130.00 KZT, and a calculated refund of 1,170.00 KZT. The second returns CUSTOMER_DECLINED. The fee percentage is a project-configurable example, not a claim about a real provider's policy.
3. Data models, rules, and architecture
- DeliveryPipeline.Core/Domain.cs: ShipmentRequest, PackageDefinition, PricingPolicy, Quote, QuoteOutput, ShipmentRules.Validate, Catalog, QuoteService.CreateQuote, BatchProcessor.Prepare.
- DeliveryPipeline.Core/OrderLifecycle.cs: QuotedOrder, ConfirmedOrder, DispatchedOrder, and OrderLifecycle.Prepare, Confirm, Dispatch.
- DeliveryPipeline.Core/CancellationWorkflow.cs: CancelledOrder and CancellationWorkflow.Cancel.
- DeliveryPipeline.Core/Outcomes.cs: Result<T>, Option<T>, Map, Bind, and explicit lookup errors.
- DeliveryPipeline.Core/Boundaries.cs: file input and quote snapshots.
- DeliveryPipeline.Core/ReportQueries.cs, ReportExtensions.cs, ScoreFactory.cs, LazyQuoteFilters.cs: LINQ reports, custom extensions, function configuration, and deferred query processing.
- DeliveryPipeline.Core/ReportWorkflow.cs, ReportCsvExporter.cs: report pipeline and CSV export.
- DeliveryPipeline.Core/NativeReserveCalculator.cs: validated native interop adapter.
- DeliveryPipeline.App/Program.cs: command-line parsing and display (side effects at the boundary).
- DeliveryPipeline.Tests/Program.cs: deterministic assertion-based test runner.
Core business rules
- Only DRAFT shipments may be quoted. Quantity must be 1–1,000 and distance 0–10,000 km.
- Package multipliers: PARCEL 1.00, FRAGILE 1.25.
- STANDARD: 800 KZT base + 100 KZT/km. EXPRESS: 1,250 KZT base + 150 KZT/km.
- Discount codes: NONE 0%, STUDENT 10%.
- Quote formula: round((base + kmRate * distanceKm) * quantity * multiplier * (1 - discount), 2); quotes above 1,000,000 KZT fail.
- Lifecycle types expose the required order: Prepare returns QuotedOrder; Confirm requires that type and returns ConfirmedOrder; Dispatch requires ConfirmedOrder and a courier ID.
- Cancellation requires ConfirmedOrder and a fee rate between 0 and 1. The calculation does not represent an executed refund or persist a cancellation status.
4. Functional programming techniques
Required technique	Implemented location / use
Pure calculations / immutable records	ShipmentRules, QuoteService, core records, CancellationWorkflow.Cancel
Named transformation stages and composition	validation → package/service/discount lookup → price calculation → output mapping in QuoteService.CreateQuote
Higher-order function and partial application	ScoreFactory configured priority functions; ReportExtensions.WithPriorityScores accepts Func<QuoteOutput,int>
Functional errors and propagation	Result<T>, Map, Bind, Require in quote and lifecycle chains
Explicit missing lookups	Catalog returning Option<T>
Safe operation ordering	OrderLifecycle separate types plus failure-first chaining; batch all-or-nothing update
Custom extensions	ReportExtensions.ForService, WithPriorityScores
LINQ reports	ReportQueries.PriorityQuotes, RevenueByService: filter, projection, stable ordering, grouping, aggregation
Deferred processing	LazyQuoteFilters.AtLeast with yield return; materialise snapshots at a deliberate ToArray boundary
Persistence boundaries	JsonShipmentSource, ReportCsvExporter; missing/malformed file failures
C interop	native/deliveryfees.c via NativeReserveCalculator and P/Invoke


5. Native C integration
Source files: native/deliveryfees.c, native/deliveryfees.h. The native function delivery_reserve_minor calculates a percentage reserve in integer minor units; NativeReserveCalculator calls it through P/Invoke in the actual revenue report.

Bundled binaries:
- native/runtime/win-x64/deliveryfees.dll
- native/runtime/linux-x64/libdeliveryfees.so
Build from native source if needed:
# Windows PowerShell, requires MinGW-w64 GCC on PATH
.\scripts\build-native-windows.ps1
# Linux x64, requires GCC
./scripts/build-native.sh
The .csproj files copy the relevant native binary to the .NET output directory. Test 30 exercises an actual native calculation: 2% of 1,800.00 KZT = 36.00 KZT. Tests also cover invalid input and a controlled native-adapter failure.
6. Tests and observed results
The Windows runner completed 48/48 PASS on 14 November 2026. Its 48 tests include:
- 01–20: pricing, validation, Result/`Option`, failure propagation and persistence.
- 21–29: custom extensions, configured priority scoring, report sorting/grouping, and lazy evaluation.
- 30–40: native integration, report workflow, file failures, atomicity, and resource handling.
- 41–44: successful dispatch, declined confirmation, missing courier, and invalid order.
- 45–48: cancellation refund, boundary fee rates, invalid fee rate, and declined confirmation.
File failures must not silently produce a successful report. Failure handling and successful export are tested separately. The report exporter uses StreamWriter with deterministic disposal.
7. Repository and submission
Repository: https://github.com/kemelnurasyl/DeliveryPipeline-Final-IT2505
Branch: main
Initial published commit: e9aedfb
Final commit: To be recorded from git rev-parse HEAD after all final changes are published.

Before uploading to Moodle:
1. Verify the instructor can access the repository.
2. Build and run all tests after final modifications.
3. Add the final group project report and individual contribution report; an individual project still requires both.
4. Make sure each report truthfully identifies contributions, tests, limitations, and any permitted AI assistance.
5. Make the final Git commit and push; record its exact hash, branch, and URL in the submitted reports and submission notes.
6. Prepare a ZIP with source, .sln, .csproj, native C source/binaries or build scripts, sample data, README, and both reports. Exclude bin, obj, .git, credentials, and IDE caches. The Moodle ZIP must correspond to the version committed to Git.
8. Limitations and academic integrity
- Command-line only, no database, API, GUI, authentication, or persistent per-order state machine.
- Lifecycle and cancellation use a new in-memory DRAFT request per run; neither command persists a confirmed/dispatched/cancelled record or performs a real money transfer.
- The cancellation fee is a chosen demonstration rule.
- The number of distinct meaningful workflows must be confirmed against the final assessment brief; separate commands and steps in the same lifecycle do not automatically count as independent workflows.
- Code and report descriptions must match what the submitted code actually does.
- The project's development has involved coding assistance. Any AI use must be disclosed if and as permitted by the course syllabus and instructor. The student remains responsible for understanding and defending all code, tests, and design decisions.
Reference materials: course Assignments 5–8, Final Project Requirements, and supplied sample data. No external NuGet packages are required.