# HomeUtilities

Utility meters, bills and payments tracker for multiple properties (Windows and Android; iOS later).
Built with .NET MAUI.

**Status: early development.**

## Build

1. Install Visual Studio 2022 17.14+ (or Visual Studio 2026) with the **.NET MAUI** workload.
2. Open `HomeUtilities.sln`.
3. Set `HomeUtilities.App` as the startup project and select the **Windows Machine** target.
4. Press F5.

## Tests

```
dotnet test HomeUtilities.Tests.slnf
```

The solution filter contains everything that builds on any OS without MAUI workloads.
On Linux, `scripts/setup-dev.sh` installs the .NET 10 SDK.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [Progress](docs/PROGRESS.md)

## Licence

See [LICENSE](LICENSE).
