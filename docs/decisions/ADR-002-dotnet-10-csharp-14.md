# ADR-002: .NET 10 and C# 14

- Status: Accepted
- Date: 2026-07-20

## Decision

Use stable .NET 10 and C# 14. Pin the installed stable SDK in `global.json`, allow compatible newer .NET 10 feature bands, and disallow prerelease SDK selection. Preview SDKs and packages are prohibited.

Package versions are managed centrally. Updates must remain within supported stable releases, pass restore/build/test/format verification, and be documented when they change runtime or contract behavior.
