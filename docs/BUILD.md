# MiniTrayMon — Build Guide

How to compile MiniTrayMon from source using only the C# compiler that ships with
Windows. No Visual Studio, MSBuild, NuGet, or SDK install required.

## Prerequisites

- **Windows 10 or 11** (64-bit)
- **.NET Framework 4.x** — preinstalled on all supported Windows versions.
- **The C# compiler** at:
  ```
  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
  ```
  Verify it exists:
  ```cmd
  dir C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
  ```

That's it. MiniTrayMon has no third-party NuGet dependencies and no external `.dll`
references beyond the .NET Framework's standard assemblies (`System.dll`,
`System.Drawing.dll`, `System.Windows.Forms.dll`), which `csc.exe` references by
default.

## Source layout

All sources live under `src\`:

```
src\
├── Program.cs          entry point + single-instance mutex
├── TrayApp.cs          main form, NotifyIcon, timer, context menu
├── StatsCollector.cs    performance-counter sampling + RAM via GetPerformanceInfo
├── IconRenderer.cs     64×64 icon rendering for 3 display modes
├── OverlayForm.cs      left-click live-stats overlay window
└── Properties.cs       display-mode persistence (minitraymon.settings)
```

There is no `.csproj`, no `.sln`, and no `AssemblyInfo.cs`. Assembly metadata is
not required for `csc.exe` to produce a working executable.

## Quick build

The repository ships a build script that handles everything:

```cmd
build.bat
```

This compiles all `src\*.cs` files into `release\MiniTrayMon.exe` with optimizations
enabled. Output is typically ~22 KB.

## Manual build

From the repository root, in a plain Command Prompt (`cmd.exe`):

```cmd
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

%CSC% /target:winexe /out:release\MiniTrayMon.exe /nologo /optimize+ /warn:4 ^
    /define:RELEASE;TRACE ^
    /win32icon:NUL ^
    src\Program.cs src\TrayApp.cs src\StatsCollector.cs src\IconRenderer.cs src\OverlayForm.cs src\Properties.cs
```

If `release\` does not exist, create it first:

```cmd
if not exist release mkdir release
```

### What the flags mean

| Flag                          | Purpose                                                      |
|-------------------------------|--------------------------------------------------------------|
| `/target:winexe`              | Build a Windows GUI executable (no console window on launch)|
| `/out:release\MiniTrayMon.exe`| Output path                                                  |
| `/nologo`                     | Suppress the compiler version banner                        |
| `/optimize+`                  | Enable compiler optimizations                                |
| `/warn:4`                     | Maximum warning level                                       |
| `/define:RELEASE;TRACE`       | Compile with `RELEASE` and `TRACE` symbols defined          |
| `/win32icon:NUL`              | Suppress the default .ico (we draw the tray icon at runtime) |

### Building with a wildcard

`csc.exe` does support wildcards, so the following also works:

```cmd
%CSC% /target:winexe /out:release\MiniTrayMon.exe /nologo /optimize+ /warn:4 ^
    /define:RELEASE;TRACE /win32icon:NUL ^
    src\*.cs
```

This is what `build.bat` uses internally (it expands the wildcard itself for clarity).

## Verifying the build

```cmd
release\MiniTrayMon.exe
```

An icon should appear in the taskbar notification area within ~1.5 seconds. If
nothing happens, check Task Manager for an existing `MiniTrayMon.exe` process —
the single-instance mutex will silently suppress a second launch.

## C# 5 compatibility notes

**Important:** the .NET Framework 4.x C# compiler (`csc.exe` v4.0.30319) supports
**C# 5.0** language features. It does **not** support C# 6+ syntax. When editing
the source, you must avoid:

- ❌ **String interpolation** — no `$"..."`.
  ```csharp
  // ❌ C# 6+
  string s = $"CPU: {cpu}%";

  // ✅ C# 5 — use string.Format or concatenation
  string s = string.Format("CPU: {0}%", cpu);
  string s = "CPU: " + cpu + "%";
  ```

- ❌ **Null-conditional operators** — no `?.` or `?[]`.
  ```csharp
  // ❌ C# 6+
  int? len = obj?.Length;

  // ✅ C# 5 — explicit null check
  int? len = obj != null ? obj.Length : (int?)null;
  ```

- ❌ **Null-coalescing assignment** — no `??=` (C# 8).
- ❌ **Expression-bodied members** — no `=>` for properties/methods (C# 6).
  ```csharp
  // ❌ C# 6+
  public int Area => width * height;

  // ✅ C# 5
  public int Area { get { return width * height; } }
  ```
  (Lambda expressions `=>` for delegates are fine — those are C# 3.)

- ❌ **`nameof`** expressions (C# 6).
- ❌ **`using static`** (C# 6).
- ❌ **Auto-property initializers** — `public int X { get; set; } = 5;` (C# 6).
- ❌ **Read-only auto-properties** — `public int X { get; }` (C# 6).
- ❌ **`switch` expressions** (C# 8).
- ❌ **Target-typed `new`** — `Foo f = new();` (C# 9).

### Features that ARE available (C# 5)

- `async` / `await` (not used in MiniTrayMon, but supported)
- Lambda expressions `=>`
- `var`, object initializers, collection initializers
- Extension methods
- `dynamic`
- Named and optional arguments
- `string.Format`, `+` concatenation, `string.Concat`

The existing MiniTrayMon source deliberately uses C# 5 idioms throughout
(`string.Format`, `+` concatenation, full property bodies). Keep this convention
when contributing.

## Troubleshooting the build

**`'csc.exe' is not recognized as an internal or external command`**
You didn't set the `CSC` variable, or you're running from PowerShell and the `^`
line-continuation doesn't work. Use `cmd.exe`, or write the command on a single line.

**`error CS0234: The type or namespace name 'Drawing' does not exist`**
The `System.Drawing` and `System.Windows.Forms` assemblies are referenced by
default by `csc.exe` for `/target:winexe`. If you removed the `/target:winexe`
flag, add explicit references:
```cmd
/reference:System.Drawing.dll,System.Windows.Forms.dll
```

**`error CS1617: Invalid option '7.3' for /langversion`**
You probably have a stray `/langversion` flag. Remove it — `csc.exe` v4.0.30319
defaults to C# 5, which is exactly what we want.

**Build succeeds but the exe is ~4 KB instead of ~22 KB**
You probably forgot to include one of the source files. List all six `.cs` files
explicitly (see the manual build command above).

**Antivirus flags the output as suspicious**
A tiny self-contained `.exe` with no signature will occasionally trip heuristic
detection. This is a false positive. Sign the executable with a code-signing
certificate if distributing it widely.

## Build matrix

| Build            | Command           | Output                            | Use when               |
|------------------|-------------------|-----------------------------------|------------------------|
| Release          | `build.bat`       | `release\MiniTrayMon.exe` (~22 KB) | Distribution           |
| Debug (manual)   | drop `/optimize+`, replace `/define:RELEASE` with `/define:DEBUG` | `release\MiniTrayMon.exe` | Local debugging — attach a debugger to the running process |

MiniTrayMon does not currently emit a `.pdb`; add `/debug:full` to get one if you need
line numbers in stack traces.
