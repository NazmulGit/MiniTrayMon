# MiniTrayMonPran (MiniTrayMon) — Tiny Windows Tray Monitor for CPU, RAM, Disk & Network

**MiniTrayMonPran** is a lightweight Windows system-tray monitor that draws a live 64×64 icon of CPU, RAM, disk and network activity right in the notification area. One tiny executable (~22 KB), no runtime beyond the .NET Framework that ships with Windows 10/11, about 45 MB RAM.

- **Live icon styles:** numeric row, vertical bars, CPU-only variants and four icon scales.
- **Zero install dependencies**, single self-contained exe, 1.5 s poll interval, start-up installer script.
- **Current release:** v1.5.1 · license MIT

> Status: private repository.

---


**A lightweight Windows system tray monitor for CPU, RAM, Disk, and Network activity.**

MiniTrayMon lives in your taskbar notification area and renders a live, constantly-updated
64×64 icon showing system resource usage at a glance. It is a single self-contained
executable with no runtime dependencies beyond the .NET Framework shipped with Windows.

- **Target:** Windows 10 / 11 (64-bit)
- **Runtime:** .NET Framework 4.x (preinstalled on Windows 10/11)
- **Executable size:** ~22 KB
- **Memory footprint:** ~42–53 MB resident RAM
- **Poll interval:** 1500 ms
- **Version:** 1.0.0
- **License:** MIT

![MiniTrayMon screenshot](docs/screenshot.png)
<!-- Screenshot placeholder — drop a 320×128 PNG of the three icon modes here. -->

---

## Features

- **Taskbar speed meter** — an always-on-top, click-through overlay on the taskbar
  showing live values for ALL resources: CPU %, RAM %, Disk %, and Network ↓/↑ speeds.
  Toggle via right-click → "Show speed meter on taskbar". Persists across restarts.
- **Three tray-icon display modes**, switchable from the right-click menu:
  - **Bars** — 4 colored horizontal bars (CPU / RAM / Disk / Network) with percentage labels. *Default.*
  - **Numeric** — 2×2 grid of large percentage numbers, one per metric.
  - **Overall** — a single circular gauge showing a weighted combined system-load percentage, similar to Task Manager's summary view.
- **Left-click overlay** — a borderless always-on-top live stats window with detailed
  numbers (GB used/free, MB/s read/write, Mbps up/down). Auto-closes after 15 seconds.
- **Right-click context menu** — live numeric readouts for every metric, display-mode
  switching, About, and Exit.
- **Persistent mode** — your chosen display mode is saved to
  `minitraymon.settings` next to the executable and restored on next launch.
- **Single-instance** — a named mutex (`Global\MiniTrayMon_SingleInstance_v1`)
  prevents duplicate instances; launching a second time silently exits.
- **Zero dependencies** — no installer, no DLLs, no registry writes, no admin rights.
  Drop the `.exe` anywhere and run.

---

## Installation

### Option A — Prebuilt release

1. Download `MiniTrayMon.exe` from the `release/` directory (or the latest release archive).
2. Place it in any folder, e.g. `C:\Tools\MiniTrayMon\MiniTrayMon.exe`.
3. Double-click to run. An icon appears in the taskbar notification area.

### Option B — Run at Windows startup

Use the included `install_startup.bat` to drop a shortcut into the Windows Startup folder
(`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`):

1. Place `install_startup.bat` in the same folder as `MiniTrayMon.exe`
   (or in the parent folder of a `release\` subdirectory containing the exe).
2. Double-click `install_startup.bat`.
3. A console window confirms success and exits on its own. MiniTrayMon will now launch
   automatically the next time you log in.

To undo, delete the shortcut from `shell:startup` (paste that into the address bar of
File Explorer).

### Option C — Build from source

See **[docs/BUILD.md](docs/BUILD.md)** for the exact `csc.exe` command and C# 5
compatibility notes.

---

## Usage

### The tray icon

The icon itself *is* the dashboard. It refreshes every 1.5 seconds. Each display mode
packs the same underlying data into a different visual density:

| Mode      | What you see                                                         | Best for                       |
|-----------|----------------------------------------------------------------------|--------------------------------|
| **Bars**  | 4 colored bars + `%` labels + small CPU number + net throughput tag | At-a-glance monitoring          |
| **Numeric** | 2×2 grid of large numbers (C / R / D / N), turns red-orange over 80% | Reading exact percentages      |
| **Overall** | Single circular gauge, color shifts green → orange → red           | A single "how busy is my PC"   |

### Left-click — overlay window

Left-clicking the tray icon opens a 340×260 always-on-top live stats panel anchored to
the bottom-right corner of your primary screen. It shows:

- CPU % (1 decimal)
- Memory %, used / total / free in GB
- Disk %, plus read / write / total throughput in both MB/s and KB/s, and Mbps
- Network download (↓) and upload (↑) in Mbps
- Overall combined %
- A countdown — the window auto-closes after 15 seconds

### Right-click — context menu

| Menu item            | Action                                                       |
|----------------------|--------------------------------------------------------------|
| Show live overlay    | Opens the same overlay as left-click                         |
| Display mode ▸ Bars  | Switch to Bars mode (persists)                               |
| Display mode ▸ Numeric | Switch to Numeric mode (persists)                           |
| Display mode ▸ Overall | Switch to Overall mode (persists)                           |
| CPU / Memory / Disk / Network / Overall | Read-only live readouts, refreshed each poll |
| About                | Version info dialog                                          |
| Exit                 | Quit MiniTrayMon                                             |

### Keyboard / shortcuts

MiniTrayMon is intentionally mouse-driven and has **no global hotkeys** (that would
require a low-level keyboard hook — exactly the kind of overhead this tool avoids).

The only implicit "shortcut" is **left-click = open overlay**.

---

## File structure

```
MiniTrayMon/
├── README.md                  ← this file
├── LICENSE                    ← MIT license
├── build.bat                  ← release build script (csc.exe)
├── install_startup.bat        ← drops a shortcut into shell:startup
├── src/
│   ├── Program.cs             ← entry point + single-instance mutex
│   ├── TrayApp.cs             ← main form: NotifyIcon, timer, menu, orchestration
│   ├── StatsCollector.cs      ← PerformanceCounter + GetPerformanceInfo sampling
│   ├── IconRenderer.cs        ← 64×64 icon rendering for the 3 display modes
│   ├── OverlayForm.cs         ← left-click live-stats overlay window
│   └── Properties.cs          ← display-mode persistence (minitraymon.settings)
├── docs/
│   ├── USAGE.md               ← detailed usage + metric reference
│   ├── BUILD.md               ← compile-from-source instructions
│   └── screenshot.png         ← (placeholder)
├── build/                     ← intermediate build artifacts (empty / scratch)
└── release/
    └── MiniTrayMon.exe        ← compiled binary (~22 KB)
```

---

## Resource comparison vs. Windows Task Manager

| Property                 | MiniTrayMon                          | Task Manager (Taskmgr.exe)        |
|--------------------------|--------------------------------------|------------------------------------|
| Executable size          | ~22 KB                               | ~1.9 MB (Windows 11)              |
| Resident RAM             | ~42–53 MB                            | ~80–150 MB (tab-dependent)         |
| Always-on-top mini view  | Yes (taskbar icon + 15s overlay)     | No (full window or nothing)        |
| Permanent tray presence  | Yes — icon is the dashboard          | No tray icon; runs only when opened |
| Per-poll cost            | 6 PerformanceCounter reads + 1 P/Invoke | Enumerates every process, draws charts |
| Admin rights required    | No                                   | Only for "show processes from all users" |
| Telemetry / online calls| None                                 | None                               |

MiniTrayMon is **not** a replacement for Task Manager's process-level detail. It is a
zero-friction ambient monitor: glance at the taskbar, see CPU/RAM/Disk/Network, move on.

---

## Troubleshooting

**The icon does not appear in the tray.**
Windows 11 may hide new tray icons in the overflow area. Click the `^` arrow in the
taskbar corner, drag `MiniTrayMon` up to the taskbar, and pin it. Or:
*Settings → Personalization → Taskbar → Other system tray icons → MiniTrayMon → On*.

**Network always reads 0 Mbps.**
MiniTrayMon auto-selects the first non-loopback network interface it finds. If you have
multiple adapters (e.g. VPN, virtual switches from Hyper-V/WSL), it may pick the wrong
one. Disconnect or disable unused virtual adapters, or rebuild from source and hard-code
the interface name in `StatsCollector.GetFirstNetworkInstance()`.

**Disk % sits at 100% even when idle.**
The `% Disk Time` performance counter is known to over-report on some NVMe/SAN setups.
MiniTrayMon falls back to a throughput-based estimate when `% Disk Time` returns 0, but
if it returns a wrong nonzero value, the bar will be wrong. This is a known limitation
of the underlying Windows performance counter, not a bug in MiniTrayMon.

**CPU shows 0% on first launch.**
This is expected. The Windows CPU performance counter needs two samples to compute a
delta. MiniTrayMon discards the first sample and reports 0% until the second poll
(~1.5 seconds after launch).

**Memory shows much less than Task Manager.**
MiniTrayMon reports **physical memory** (used = total − available, via
`GetPerformanceInfo`). Task Manager's "Memory" number includes standby/cached memory
differently. Both are correct; they measure slightly different things.

**The overlay closes too fast.**
The overlay auto-closes after 15 seconds by design (it is a glance panel, not a dock).
Re-open it with a left-click. To change the timeout, edit
`OverlayForm.cs` line `_remaining = 15;` and rebuild.

**"Already running" or nothing happens on launch.**
MiniTrayMon enforces a single instance via a named global mutex. If the process crashed
without releasing it, the mutex is still held for a few seconds by Windows. Wait 30
seconds and try again, or check Task Manager for an orphaned `MiniTrayMon.exe` process.

**I want to fully uninstall.**
Delete the executable, delete the shortcut in `shell:startup` (if you used
`install_startup.bat`), and optionally delete `minitraymon.settings` next to where the
exe lived. There are no registry entries, no appdata folders, no leftover DLLs.

---

## License

MIT License. See [LICENSE](LICENSE).

Copyright © 2026 MiniTrayMon contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
to whom the Software is furnished to do so.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.

---

## Keywords
windows tray monitor, system resource monitor, cpu ram tray icon, lightweight system monitor, csharp winforms, taskbar monitor
