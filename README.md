
# DeliveryPipeline Final Project

## Project Information

**Course:** Introduction to Functional Programming (IT-2505)  
**Student:** Kemel Nurassyl  
**Platform:** C# / .NET 8  
**Application:** Console Application  
**Repository:** https://github.com/kemelnurasyl/DeliveryPipeline-Final-IT2505  
**Branch:** main  
**Initial Commit:** e9aedfb

## 1. Project Description

DeliveryPipeline is a C# application designed to automate delivery price calculations, validate orders, manage delivery workflows, calculate refunds, and generate financial reports.

The application demonstrates functional programming principles, including pure functions, immutable models, pattern matching, higher-order functions, Result and Option types, LINQ, lazy evaluation, and native C integration.

### Main Features

1. Delivery quotation and order validation
2. Order confirmation and dispatch
3. Order cancellation and refund calculation
4. Priority scoring with configurable policies
5. Revenue reports and CSV export
6. JSON data loading and validation
7. Native C library integration
8. Automated testing

## 2. Technologies Used

- C#
- .NET 8 SDK
- Functional Programming
- LINQ
- JSON and CSV
- Native C and P/Invoke
- Git and GitHub

No external NuGet packages are required.

## 3. Project Structure

```text
DeliveryPipeline_Final/
|
|-- DeliveryPipeline.App/
|   |-- Program.cs
|
|-- DeliveryPipeline.Core/
|   |-- Domain.cs
|   |-- OrderLifecycle.cs
|   |-- CancellationWorkflow.cs
|   |-- Outcomes.cs
|   |-- Boundaries.cs
|   |-- ReportQueries.cs
|   |-- ReportExtensions.cs
|   |-- ReportWorkflow.cs
|   |-- ReportCsvExporter.cs
|   |-- NativeReserveCalculator.cs
|   |-- ScoreFactory.cs
|   |-- LazyQuoteFilters.cs
|
|-- DeliveryPipeline.Tests/
|   |-- Program.cs
|
|-- native/
|-- samples/
|-- reports/
|-- scripts/
|-- DeliveryPipeline.sln
|-- README.md
```

## 4. Installation and Execution

### Requirements

- .NET 8 SDK
- Windows x64 or Linux x64
- Visual Studio Code or Visual Studio

### Check .NET Version

```powershell
dotnet --version
```

### Build Application

```powershell
dotnet build DeliveryPipeline.sln
```

### Run Automated Tests

```powershell
dotnet run --project DeliveryPipeline.Tests
```

**Verified result: 48/48 tests passed.**

## 5. Main Workflows

### Workflow 1: Delivery Pricing and Reports

The application reads orders from JSON, validates them, calculates delivery quotations, generates financial reports, and exports results to CSV.

```powershell
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/final-report.csv routine
```

Example results:

- Successful quotations: 6
- Total value: 11,150 KZT
- EXPRESS revenue: 6,750 KZT
- STANDARD revenue: 4,400 KZT
- Total operational reserve: 223 KZT

### Workflow 2: Order Confirmation and Dispatch

Orders must be validated and confirmed before dispatch.

```powershell
dotnet run --project DeliveryPipeline.App -- lifecycle L1 5 true COURIER-01
```

Expected output:

```text
SUCCESS: Order 'L1' dispatched to courier 'COURIER-01'.
```

Customer rejection:

```powershell
dotnet run --project DeliveryPipeline.App -- lifecycle L2 5 false COURIER-01
```

Expected output:

```text
FAILURE [CUSTOMER_DECLINED]:
The customer did not accept the delivery quote.
```

### Workflow 3: Cancellation and Refund

The application calculates a cancellation fee and refund for a confirmed order.

```powershell
dotnet run --project DeliveryPipeline.App -- cancel L3 5 true 0.10
```

Expected output:

```text
SUCCESS: Order 'L3' cancelled.
Original: 1300.00 KZT
Cancellation fee: 130.00 KZT
Refund: 1170.00 KZT
```

The 10% cancellation fee is an illustrative project policy.

### Important Limitation

The lifecycle and cancellation workflows operate on newly created in-memory requests.

They do not persist order status changes or perform actual financial transfers.

The instructor determines whether the demonstrated scenarios meet the required number of distinct meaningful workflows.

## 6. Configurable Priority Policies

The application supports two priority policies.

### Routine Policy

```powershell
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/report.csv routine
```

### Strict Policy

```powershell
dotnet run --project DeliveryPipeline.App -- samples/report-orders.json reports/strict-report.csv strict
```

For orders B and C:

- Routine priority: 10
- Strict priority: 1

These policies modify priority scores without changing delivery prices.

## 7. Functional Programming Techniques

### Pure Functions

Core calculations and validation are separated from console input and file operations.

### Immutable Models

The application uses immutable domain records and transformations.

### Pattern Matching

Pattern matching is used to handle success, failure, and domain decisions.

### Result and Option

Result represents successful and failed operations.

Option represents available and missing values.

### Map and Bind

Map transforms successful results.

Bind connects dependent operations and stops execution when a failure occurs.

### Higher-Order Functions

Functions can be passed as arguments to configure business rules.

### Currying and Partial Application

The priority scoring system supports configured functions using partial application.

### LINQ

The application generates priority and revenue reports using filtering, projection, sorting, grouping, and aggregation.

### Lazy Evaluation

The application uses IEnumerable and yield return for deferred processing.

### Safe Operation Sequence

Order lifecycle types restrict valid operation ordering:

QuotedOrder -> ConfirmedOrder -> DispatchedOrder

## 8. Native C Integration

The application includes a native C library.

Files:

- native/deliveryfees.c
- native/deliveryfees.h
- native/runtime/win-x64/deliveryfees.dll
- native/runtime/linux-x64/libdeliveryfees.so

The C library calculates an operational reserve.

The native function is called from C# through P/Invoke.

Example:

```text
Order value: 1800 KZT
Reserve rate: 2%
Calculated reserve: 36 KZT
```

### Build Native Library on Windows

Requires MinGW-w64 GCC:

```powershell
.\scripts\build-native-windows.ps1
```

### Build Native Library on Linux

Requires GCC:

```bash
./scripts/build-native.sh
```

## 9. Automated Testing

The project includes 48 automated tests.

### Tests 01-20

- Pricing calculations
- Domain validation
- Result and Option
- Map and Bind
- Failure propagation
- Persistence and invalid input

### Tests 21-29

- Extension methods
- Priority policies
- LINQ reports
- Grouping and sorting
- Lazy evaluation

### Tests 30-40

- Native C integration
- Report generation
- CSV export
- File handling
- Resource disposal
- Failure recovery

### Tests 41-44

- Successful order dispatch
- Customer rejection
- Missing courier
- Invalid order

### Tests 45-48

- Refund calculation
- Boundary fee rates
- Invalid cancellation fee
- Rejected confirmation

**Final observed test result: 48/48 PASS**

The tests use a deterministic assertion-based console runner.

## 10. Error Handling

The application provides explicit errors, including:

- INVALID_QUANTITY
- PACKAGE_NOT_FOUND
- INVALID_DISTANCE
- CUSTOMER_DECLINED
- COURIER_REQUIRED
- INVALID_FEE_RATE

Invalid operations return failures without executing dependent stages.

## 11. Business Value

The application helps delivery operations:

- Calculate consistent delivery quotations
- Reduce manual pricing errors
- Prioritise orders
- Enforce validation rules
- Calculate cancellation refunds
- Analyse revenue by service
- Estimate operational reserves

Revenue reports support financial and operational decision-making.

The 2% reserve is an illustrative internal business calculation.

## 12. Known Limitations

- Console interface only
- No database
- No authentication
- No cloud integration
- No persistent order lifecycle history
- No real payment or refund processing
- Cancellation rules are illustrative

## 13. Repository Information

**GitHub:** https://github.com/kemelnurasyl/DeliveryPipeline-Final-IT2505

**Branch:** main

**Initial Commit:** e9aedfb

**Final Submission Commit:** To be recorded after final documentation and reports are completed.

## 14. Final Submission

The final submission must contain:

1. Complete C# application
2. Native C source and build materials
3. Automated tests
4. Sample data
5. README.md
6. Group Project Report
7. Individual Contribution Report
8. GitHub repository information

Generated bin and obj directories must be excluded from the submitted ZIP.

## 15. Academic Integrity

The project builds on course Assignments 5-8.

Development has involved AI coding assistance. Any permitted AI assistance must be acknowledged according to the instructor's requirements.

The student is responsible for understanding, explaining, testing, and defending all submitted code.

## 16. References

- Introduction to Functional Programming course materials
- Assignments 5-8
- Final Project Requirements
- Microsoft .NET 8 documentation
