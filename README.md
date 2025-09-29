# CsprojPackageConflictsAnalyzer
Utility for analyzing a C# solution for NuGet package version conflicts **totally locally** (no network calls, using only local nuget cache).

---

## Why?

Large .NET solutions often drift into “DLL hell”: the same package appears in different versions across projects, transitive dependencies pull in unexpected versions.
This tool scans a `.sln` and its `.csproj` files to surface those inconsistencies fast. It uses only local Nuget cache, transitive dependencies analyzed through it. 

---

## What it does

- Builds a **project ↔ package** map from your `.sln` and `.csproj` files (no restore required).
- Detects **version conflicts** of the same package across projects.
- Distinguishes **direct vs transitive** packages to highlight the real source of conflicts.
- Prints a **human-readable table** to the console and can **export CSV** for audits (e.g., attach to a PR).
- Works offline on Windows, macOS, and Linux with the .NET SDK.
---

## Quick start

### Prerequisites
- [.NET SDK 6.0+](https://dotnet.microsoft.com/en-us/download) installed.
- A C# solution (`.sln`) you want to analyze.

### Build & run

```bash
# 1) Clone
git clone https://github.com/pekishev/CsprojPackageConflictsAnalyzer.git
cd CsprojPackageConflictsAnalyzer

# 2) Build (Release recommended)
dotnet build -c Release

# 3) Run against your solution
dotnet run --project ParseCsProj1.csproj -- "path/to/YourSolution.sln"
# or, if the default project is set:
# dotnet run -- "path/to/YourSolution.sln"
