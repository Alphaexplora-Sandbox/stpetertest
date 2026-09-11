# stpetertest

Created by ALPHACI as a .NET service standalone repository.

This starter already includes source files, package scripts, TypeScript, ESLint, Jest coverage, SonarQube metadata, branch protections, and ALPHACI workflow files that match the selected stack. Use it as the first working baseline, then replace the starter code with your application code.

## Project structure

- `src/<Project>/Program.cs` boots the minimal API and exposes `/health`, which the production gate probes after each deployment.
- `tests/<Project>.Tests/` hosts the application in memory with `WebApplicationFactory`, so coverage reflects code that actually runs.
- `.editorconfig` is what `dotnet format` checks against; the pipeline runs it at warning severity on uat and main.
- `global.json` pins the SDK so local builds and CI resolve the same version.

## Branch strategy

| Branch  | Purpose |
|---------|---------|
| main    | Production - protected |
| uat     | Integration and test - protected |
| develop | Development integration - unprotected, no CI pipeline |

## CI/CD

Workflow files live in `.github/workflows/`. The CI pipeline runs on `uat` and `main` only. `develop` and user-created branches do not trigger workflows. Push to `uat` to trigger your first run.

## Getting started

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --configuration Release --no-restore
dotnet test --collect:"XPlat Code Coverage"
```

Create a feature branch, open a pull request into `dev`, and let ALPHACI promote green changes through `uat` to `main`.