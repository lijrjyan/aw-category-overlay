# ActivityWatch Category Overlay

A lightweight Windows overlay for selected ActivityWatch Top Categories. It
shows today's accumulated duration against a per-category minimum or maximum
threshold, stays above ordinary windows, and is click-through during normal use.

## Requirements

- Windows 10 or 11, x64
- ActivityWatch available at `http://localhost:5600`
- .NET 8 Windows Desktop Runtime

The current machine already has the required runtime.

## Build and test

From WSL:

```bash
dotnet build aw-category-overlay/ActivityWatch.CategoryOverlay.sln
dotnet test aw-category-overlay/ActivityWatch.CategoryOverlay.sln
```

Run the opt-in read-only test against the live ActivityWatch server:

```bash
AW_OVERLAY_RUN_LIVE_TESTS=1 \
dotnet test aw-category-overlay/tests/ActivityWatch.CategoryOverlay.Core.Tests \
  --filter FullyQualifiedName~ActivityWatchLiveParityTests
```

## Publish

```bash
dotnet publish \
  aw-category-overlay/src/ActivityWatch.CategoryOverlay.Windows/ActivityWatch.CategoryOverlay.Windows.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -o aw-category-overlay/artifacts/win-x64
```

Launch:

```text
aw-category-overlay/artifacts/win-x64/ActivityWatch.CategoryOverlay.exe
```

For a stable Windows-local path, copy the executable to:

```text
%LOCALAPPDATA%\ActivityWatch\CategoryOverlay\ActivityWatch.CategoryOverlay.exe
```

## Use

The application starts as a top-right semi-transparent overlay and adds a system
tray icon. The tray menu provides:

- show or hide the overlay;
- refresh now;
- enter or leave draggable edit mode;
- open settings;
- enable or disable Windows autostart;
- exit.

Normal mode is mouse-click-through. In Settings, select categories, arrange their
fixed display order, choose `Minimum` or `Maximum`, set one threshold, choose a
five- or ten-minute refresh interval, and adjust opacity.

On the first run, Settings opens automatically because no categories have been
selected yet. Subsequent launches keep the saved selection and remain
unobtrusive.

Configuration is stored at:

```text
%LOCALAPPDATA%\ActivityWatch\CategoryOverlay\config.json
```

The logical day boundary is read from ActivityWatch `startOfDay`; the user's
current server uses 04:00. The default refresh interval is five minutes.

## Data safety

The overlay reads settings, buckets, and category query results. Its only POST is
to ActivityWatch's read-only query endpoint. It never writes raw events,
`settings/classes`, buckets, or any other ActivityWatch configuration.

## Platform status

This release is Windows-only and prioritizes the current desktop experience. A
future cross-platform release may replace the WPF shell while retaining the
query, configuration, and threshold contracts.
