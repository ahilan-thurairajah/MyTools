# MyTools

A .NET 8 solution that starts with a Windows desktop app "My Calculator", a shared core library, and unit tests.

## Projects
- src/MyTools.MyCalculator: WPF app for Windows 10/11
- src/MyTools.Core: Shared core logic (Calculator)
- tests/MyTools.Tests: xUnit tests for the core

## Run
```powershell
# build solution
 dotnet build

# run the calculator app
 dotnet run --project .\src\MyTools.MyCalculator\MyTools.MyCalculator.csproj
```

## Test
```powershell
 dotnet test .\tests\MyTools.Tests\MyTools.Tests.csproj --nologo
```

## Notes
- Target framework: .NET 8
- If you prefer WinUI 3 (Windows App SDK) instead of WPF, we can add a WinUI 3 project alongside.
