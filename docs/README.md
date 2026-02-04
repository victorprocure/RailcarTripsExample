# RailcarTrips

A Blazor Web App application for displaying and processing railroad equipment trips from event data.

## Overview

This application processes equipment events (CSV format) into structured trip records. It demonstrates:
- **Event Processing**: Parses CSV equipment events and matches "W" (start) and "Z" (end) events into trips
- **Timezone Handling**: Converts local event times to DateTimeOffset based on city timezone information
- **Database Persistence**: Stores trips, events, equipment, and cities using Entity Framework Core
- **Web Interface**: Blazor Web App UI
- **Structured Logging**: Console-based logging using Microsoft.Extensions.Logging for diagnostics

## Key Assumptions

1. **Event Times**: Event times in the CSV are assumed to be in the local timezone of their corresponding city. The application converts these to a DateTimeOffset after converting the Time Zone string from the city data into a valid ZoneId. I then store the datetime offset.
As per the instructions, I then display this as UTC.
*Note* this is probably a bit over engineered, I could've just converted to UTC and stored that. However DateTimeOffset has a lot more flexibility moving forward.

2. **Trip Pairing Logic**: Events are assumed to be paired as follows:
   - Event code **W** (Released) marks the start of a trip
   - Event code **Z** (Placed) marks the end of a trip
   - All events between a W and Z event are considered part of that trip
   - If a W event has no matching Z event, the import will fail with an error, again this is an assumption I made. As I would think all start events should have a corresponding end event.

3. **Data Seeding**: Cities and equipment reference data are pre-populated via EF Core migrations. These do not require user intervention through the UI.

4. **Duplicate Handling**: The system checks for duplicate trips (same equipment, start time, end time) and skips creation if they already exist in the database.

## Dependencies

### System
  - **Dotnet 10**: Required for running the application
  - **SQL Server**: Required for data persistence
    - SQL Server Developer Edition (recommended for development)
    - SQL Server LocalDB (supported but only tested with Docker setup)
  - **Docker Desktop**: Optional, for containerized SQL Server setup (see [database guide](./guides/sql-server.md))

### NuGet Packages
  - Microsoft.EntityFrameworkCore.SqlServer (10.0.2)
  - Microsoft.EntityFrameworkCore.Design (10.0.2)

## Setup Instructions

### 1. Prerequisites

Ensure you have the following installed:
```bash
dotnet --version  # Should show .NET 10.0.x
```

### 2. Configure SQL Server

#### Option A: Docker (Recommended)
Follow the [SQL Server Docker guide](./guides/sql-server.md) to set up a containerized SQL Server instance.

#### Option B: Local SQL Server / LocalDB
Ensure SQL Server is running and accessible. The default connection string in `appsettings.json` is:
```
"DefaultConnection": "Server=(local);Database=RailcarTripsDb;Trusted_Connection=true;TrustServerCertificate=true;"
```

You can override this via User Secrets for development:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
```

### 3. Apply Database Migrations

#### Automatic Migration (Default)
The application automatically applies migrations on startup via code in `Program.cs`:
```csharp
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<RailcarTripsContext>();
await dbContext.Database.MigrateAsync();
```

This happens automatically when the app starts, so no manual intervention is required.

#### Manual Migration
To manually apply migrations (useful for deployment):
```bash
# Apply all pending migrations
dotnet ef database update --project src/RailcarTrips/RailcarTrips.csproj
```

To create a new migration after modifying EF Core models:
```bash
dotnet ef migrations add MigrationName --project src/RailcarTrips/RailcarTrips.csproj
```

### 4. Build and Run

```bash
# Build the solution
dotnet build altagas-assessment.slnx

# Run the application (starts on https://localhost:5001 by default)
dotnet run --project src/RailcarTrips/RailcarTrips.csproj
```

Alternatively, use VS Code tasks for a better development experience:
```bash
# VS Code Build task
Ctrl+Shift+B → Select "build"

# VS Code Watch task (rebuilds on file changes)
Ctrl+Shift+B → Select "watch"
```

### 5. Running Tests

Unit tests are located in `tests/RailcarTrips.Tests/`. Run them with:
```bash
dotnet test altagas-assessment.slnx

# Or run specific test file
dotnet test tests/RailcarTrips.Tests/CsvParserTests.cs
```

## Application Architecture

### Key Services

- **TripsImportService**: Orchestrates CSV import, validation, and event persistence
- **TripProcessor**: Extracts trips from events (W→Z pairing), handles deduplication
- **TripsService**: Queries trips with related data, handles sorting
- **CsvParser**: Parses CSV streams with header skipping and empty row filtering

## Logging

The application uses **Microsoft.Extensions.Logging** configured to output to the console. Logs are categorized by level:

- **Information**: Import start/completion, event counts, trip processing summaries
- **Warning**: City not found, invalid date formats, validation failures
- **Debug**: Row skipping details, individual trip creation, duplicate detection
- **Error**: Critical failures during import or transaction rollback

To adjust log level, edit `Program.cs`:
```csharp
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

## Troubleshooting

### Database Connection Issues
- Verify SQL Server is running and accessible
- Check the connection string in `appsettings.json` or user secrets matches your server
- Ensure the user has database creation permissions
- Try using `TrustServerCertificate=true` for self-signed certificates

### CSV Import Failures
- Verify CSV format: `Equipment Id,Event Code,Event Time,City Id`
- Ensure event times are parseable (e.g., `2024-01-01 10:00` or `01/01/2024 10:00:00`)
- Verify all City IDs in the CSV exist in the Cities table
- Check console logs for specific validation warnings (missing fields, invalid dates, unknown cities)
- Ensure Equipment events are not malformed or missing required fields

### Timezone Conversion Issues
- Verify cities are seeded with correct timezone IDs (e.g., `Eastern Standard Time`)
- Ensure event times in CSV are in the city's local timezone, not UTC
- Check logs for timezone-related warnings during import

## Development Notes

### Code Standards
I made the best attempt to adhere to C# 14 standards with:
- File-scoped namespaces
- Nullable reference types enabled
- XML documentation comments on public APIs
- Structured logging with named parameters
- Pattern matching and switch expressions
- `is null`/`is not null` instead of `== null`/`!= null`
- `nameof()` for property references instead of string literals

## TODO
If I wanted to spend more time, I'd containerize the whole thing, and use a docker compose file to:
  a) Automatically install SQL Server
  b) Create the database via a script
  c) Run the application
I felt this would be a bit overkill for a demo.

I would also move the migrations to a migration folder and figure out why they decided to output in the wrong directory. But there is not that many right now, so easy to navigate around.

I would also create a new migration to fix the title of two of my columns to remove "UTC" from the naming, as that's not what is stored in them. It's a dateTimeOffset, this was a remnant of a previous design idea I had.

I would also move the code in components and pages to either code-behind or move more of it to services as I did in some places already.