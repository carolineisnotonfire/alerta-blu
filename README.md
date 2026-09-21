# alerta-blu

A makeover of the AlertaBLU app, which provides helpful information for Blumenau citizens regarding weather, river and dam levels. Built with .NET MAUI, following Clean Architecture.

## Project structure

| Project | Role |
|---|---|
| `AlertaBlu.Domain` | Core types (river levels, thresholds, forecasts, dams). No dependencies. |
| `AlertaBlu.Application` | Use cases (`LoadDashboardUseCase`), ports (`IAlertaBluGateway`, `IDashboardCache`, `IClock`), and `AlertaBluOptions`. Depends only on Domain. |
| `AlertaBlu.Infrastructure` | HTTP fetching, HTML/JSON parsing, the offline file cache, and DI wiring. |
| `AlertaBlu.Presentation` | View models (plain `net10.0`, no MAUI dependency, so they're unit-testable directly). |
| `AlertaBlu` | The MAUI app itself — XAML pages, platform heads, composition root (`MauiProgram.cs`). |
| `AlertaBlu.Tests` | xUnit test suite covering Domain, Application, Infrastructure and Presentation. |

Data flows one way: `AlertaBlu` (UI) → `Presentation` → `Application` → `Domain`, with `Infrastructure` implementing the ports `Application` defines. No layer references anything further out than its own dependency arrow.

## Features

- River level, official flood-alert bands, dam reservoir status, and a 5-day weather forecast for Blumenau, SC
- Per-street flood thresholds ("cotas de enchente"), searchable
- Offline cache: the last successfully loaded data is shown (with a "dados de HH:mm" staleness indicator) if a live refresh fails
- Resilient HTTP fetching with automatic retry

## Building and testing

Requires the .NET 10 SDK with the MAUI workload installed (`dotnet workload install maui`).

```bash
# Run the test suite (plain net10.0, no MAUI workload needed for this)
dotnet test AlertaBlu.Tests/AlertaBlu.Tests.csproj

# Build the app for a specific platform
dotnet build AlertaBlu/AlertaBlu.csproj -f net10.0-windows10.0.19041.0
dotnet build AlertaBlu/AlertaBlu.csproj -f net10.0-android
```
