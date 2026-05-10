# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

reqifviewer is a Blazor Server web application for inspecting and navigating [ReqIF](https://www.omg.org/spec/ReqIF/1.2/About-ReqIF/) files. It targets **.NET 10** and depends on the [ReqIFSharp](https://reqifsharp.org) / `ReqIFSharp.Extensions` libraries for ReqIF parsing. The deployed instance is at https://viewer.reqifsharp.org.

The solution (`reqifviewer.sln`) contains two projects:
- `reqifviewer/` — the Blazor Server web app (`Microsoft.NET.Sdk.Web`)
- `reqifviewer.Tests/` — bUnit + NUnit + Moq test project (`Microsoft.NET.Sdk.Razor`)

Note: the empty `ReqifViewer.Infrastructure/` and `ReqifViewer.Infrastructure.Tests/` directories at the repo root are not part of the solution.

## Common commands

Run from the repo root unless noted otherwise.

```powershell
dotnet restore reqifviewer.sln
dotnet build reqifviewer.sln
dotnet test  reqifviewer.sln                            # run all tests
dotnet test  reqifviewer.sln --filter FullyQualifiedName~IndexPageTestFixture   # run one fixture
dotnet test  reqifviewer.sln --filter "Name=VerifyComponent"                    # run one test
dotnet run   --project reqifviewer/reqifviewer.csproj   # run the web app locally
```

Coverage (matches CI): `dotnet-coverage collect "dotnet test reqifviewer.sln --no-restore --no-build" -f xml -o coverage.xml`. The `coverlet.runsettings` file in the repo root is configured for `opencover` format if you prefer coverlet.

### Docker

Both build scripts must be run from a **Linux shell** (e.g. the GitExtensions console) and from the repository root — the Dockerfile copies `Nuget.Config` and the `reqifviewer/` project from there.

```bash
./docker-build-local.sh    <version>   # builds stariongroup/reqifviewer:{latest,version}
./docker-build-attested.sh <version>   # builds with SBOM + provenance and pushes to Docker Hub
```

Run locally: `docker run -p 8080:8080 --name reqifviewer stariongroup/reqifviewer:latest`.

### Release / autodeploy

Pushing a tag named `web-app-x.y.z` (SemVer) triggers `.github/workflows/web-app-publish-docker-container.yml` which builds and pushes the image. The production host pulls `latest` via watchtower.

## Architecture

### ReqIF lifecycle (the central abstraction)

The whole UI is driven by a single scoped service, `IReqIFLoaderService` (from `ReqIFSharp.Extensions`), registered in `Program.cs`. It holds the parsed `ReqIF` documents and raises `ReqIfChanged` when they change. Components do not load files themselves — they read `ReqIfLoaderService.ReqIFData` and subscribe to `ReqIfChanged` to re-render (see `Shared/SideMenu.razor`). Because the service is **scoped**, state lives per Blazor circuit, not globally.

The flow on `Pages/Index/IndexPage.razor[.cs]`:
1. User picks a file. It is size-checked against `MaxUploadFileSizeInMb` (from `appsettings.json`, key `Constants.MaxUploadFileSizeInMbConfigurationKey`) and streamed to disk under `wwwroot/uploads/<Guid>` — the original filename is preserved only in `fileSelectionText` so the `.reqif` / `.reqifz` extension can be detected via `ConvertPathToSupportedFileExtensionKind()`.
2. `ReqIfLoaderService.LoadAsync(...)` parses it. A `CancellationTokenSource` allows the in-flight load to be cancelled.
3. On `Dispose` / `OnClear`, the temp file is deleted via `TryDeleteFile` and the loader is `Reset()`.

When adding upload-related logic, keep this contract: the loader service owns parsed state, the page owns the temp file's lifetime.

### Routing convention

URLs follow `/reqif/{header-identifier}/<entity>/<entity-identifier>`. The `SpecElementWithAttributesExtensions.CreateUrl` helper is the canonical place for building these — extend it (don't hand-build URLs) when adding new entity types. Spec-type list pages additionally use a `?type=SpecObjectType|SpecificationType|SpecRelationType|RelationGroupType` query parameter, parsed via `Navigation/NavigationManagerExtensions.TryGetQueryString<T>`.

`SideMenu.razor` and `Pages/ReqIF/ReqIFStatistics.razor` together act as the route map; if you add a new entity page, wire it into both.

### Display-name fallback

`SpecElementWithAttributesExtensions.ExtractDisplayName` defines the precedence used everywhere a spec element is shown: `LongName` → first `AttributeValue` (XHTML rendered as `MarkupString`) → `Identifier`. Do not reinvent this in individual components.

### Layout / UI stack

- `App.razor` → `Shared/MainLayout.razor` (Radzen layout + a `BSOffCanvas` from BlazorStrap V5 for the `SideMenu`).
- UI libraries: **Radzen.Blazor**, **BlazorStrap.V5**, plus **Blazor-Analytics** (Google Analytics ID hard-coded in `Program.cs`).
- Logging: **Serilog** configured from `appsettings.json`; rolling file sink writes to `logs/log-reqifviewer-*.txt`. Use `Log.ForContext<T>()` rather than injecting `ILogger` (this is the established pattern).
- The Dockerfile runs as a non-root user (`$APP_UID`) on Alpine; anything that needs to write must go under `/app` (e.g. `/app/logs`, which is created in the image).

### Tests

Tests live next to the code they cover (`reqifviewer.Tests/Pages/...`, `reqifviewer.Tests/ReqIFExtensions/...`). They use **bUnit** (`Bunit.TestContext`) with services like `IReqIFLoaderService` mocked via Moq and `IConfiguration` built from an in-memory dictionary keyed by `Constants.MaxUploadFileSizeInMbConfigurationKey`. ReqIF sample files in `reqifviewer.Tests/TestData/` are copied to output (`CopyToOutputDirectory=Always`) — add new fixtures there, not as embedded resources.

## CI specifics that affect local repro

`.github/workflows/CodeQuality.yml` runs SonarCloud against project key `STARIONGROUP_reqifviewer` / org `stariongroup`. To match it locally you need: JDK 17, `dotnet workload install wasm-tools`, and the `dotnet-sonarscanner` + `dotnet-coverage` global tools. CI builds with `/p:ContinuousIntegrationBuild=true`.

## Contributions

Per `README.md`, external contributors must sign the CLA in the `CLA/` folder and email it to s.gerene@stariongroup.eu before PRs can be merged.
