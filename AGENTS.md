# AlSsareea Backend Agent Guide

## Project

AlSsareea is a multilingual delivery platform. This repository currently contains the backend foundation only; do not assume features that have not been requested.

## Architecture

- Maintain a modular monolith with explicit `Api`, `BuildingBlocks`, `Modules`, `Tests`, and `Docs` boundaries.
- Domain projects must not reference Application, Infrastructure, ASP.NET Core, or persistence frameworks.
- Application projects must not reference Infrastructure.
- A module must not access another module's Infrastructure project. Cross-module communication must use public contracts.
- Keep business rules in Domain or Application, never in API endpoints or Infrastructure.
- Update architecture documentation and ADRs when changing architectural decisions.

## Engineering rules

- Use stable .NET 10 and C# 14 only. Never add preview SDKs or packages.
- Keep nullable reference types, implicit usings, central package management, and warnings-as-errors enabled.
- Add packages only when required. Do not add hidden workarounds or disable tests.
- All system timestamps are UTC.
- Represent money in the smallest currency unit with `long`; never use `double` for money.
- Never store secrets in source control.
- Do not create migrations or modify a database without an explicit request.
- Do not commit, push, or create repositories unless the user explicitly asks.
- Do not change files outside the task scope.
- Preserve API compatibility; breaking contract changes require documentation and suitable versioning.
- Run restore, build, tests, and formatting verification before completing a change.

## Languages and presentation

- Supported languages are Arabic (`ar`), Hebrew (`he`), and English (`en`).
- Backend code owns culture selection and localized content concerns.
- Client applications own RTL/LTR layout and presentation direction.
