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

## Packaging
- Build the self-contained installer locally:
	- PowerShell:
		- powershell -NoProfile -ExecutionPolicy Bypass -File installer/run-installer.ps1 -Configuration Release
	- The installer EXE will be in `installer/output` and will auto-launch.
- Generate or update the app icon (teal calculator):
	- PowerShell:
		- dotnet build tools/IconGen/IconGen.csproj -c Release
		- dotnet run --project tools/IconGen/IconGen.csproj -c Release -- installer

## Releases (GitHub)
- Push a tag like `v1.0.0` to trigger GitHub Actions. It builds the app, generates the icon, compiles the Inno Setup installer, and attaches `MyTools-MyCalculator-Setup.exe` to the release.
- Download from the Releases page and run the setup to install. Start Menu: MyTools > My Calculator. Optional desktop shortcut during setup.

## Notes
- Target framework: .NET 8
- If you prefer WinUI 3 (Windows App SDK) instead of WPF, we can add a WinUI 3 project alongside.
