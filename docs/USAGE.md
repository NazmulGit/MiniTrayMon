# MiniTrayMon — Usage Guide

Detailed reference for MiniTrayMon's display modes, metrics, and behavior.

## Contents

- [Display modes](#display-modes)
  - [Bars (default)](#bars-default)
  - [Numeric](#numeric)
  - [Overall](#overall)
- [The overlay window](#the-overlay-window)
- [Metrics reference](#metrics-reference)
  - [CPU](#cpu)
  - [Memory (RAM)](#memory-ram)
  - [Disk](#disk)
  - [Network](#network)
  - [Overall %](#overall-)
- [Behavior notes](#behavior-notes)
- [Tips](#tips)

---

## Display modes

Switch modes from the right-click menu: **Display mode ▸ Bars / Numeric / Overall**.
The choice is persisted to `minitraymon.settings` next to the executable and restored
on the next launch.

The icon canvas is always **64×64 pixels**, refreshed every **1500 ms**.

### Bars (default)

Four colored horizontal bars stacked vertically, each with a `%` label on the right
and a metric tag on the left.

| Position | Metric | Color  | Tag | Bar value |
|----------|--------|--------|-----|-----------|
| 1 (top)  | CPU    | green  | CPU | raw CPU % |
| 2        | RAM    | blue   | RAM | raw RAM % |
| 3        | Disk   | orange | DSK | raw Disk % |
| 4        | Net    | purple | NET | min(100, down + 2×up) Mbps, scaled |

A small CPU integer is drawn in the top-left corner, and a net-throughput tag
(e.g. `12M` or `340K`) in the bottom-right.

**Best for:** at-a-glance ambient monitoring — colors and lengths tell the story
without reading numbers.

### Numeric

A 2×2 grid. Each cell shows a large two-digit percentage and a small letter tag
identifying the metric.

| Cell   | Tag | Metric | Color  |
|--------|-----|--------|--------|
| top-left  | C | CPU    | green  |
| top-right | R | RAM    | blue   |
| bottom-left  | D | Disk   | orange |
| bottom-right | N | Net    | purple |

The number turns **orange-red** when its value exceeds **80%**.

Network load uses the same `min(100, down + 2×up)` scaling as Bars mode so a
saturated gigabit link reads ~100 rather than a meaningless 950.

**Best for:** reading exact percentages at a small icon size.

### Overall

A single circular ring gauge (radius 24px on the 64px canvas) with a large central
percentage and an `ALL` label beneath it. The arc fills clockwise from 12 o'clock.

Color thresholds:

| Load        | Arc color  |
|-------------|------------|
| 0–49%       | green      |
| 50–79%      | orange     |
| 80–100%     | orange-red |

**Best for:** a single "how busy is my PC right now" indicator, mirroring Task
Manager's summary view.

---

## The overlay window

Open with **left-click** on the tray icon, or right-click → **Show live overlay**.

- **Size:** 340 × 260 px
- **Position:** bottom-right corner of the primary screen, 12 px inset from the edges
- **Always-on-top:** yes
- **Auto-close:** after 15 seconds (countdown shown at the bottom)
- **Refresh:** every poll cycle (1500 ms) while open

The overlay shows the most granular view of all metrics, including GB used / GB free,
MB/s and KB/s for disk, and Mbps up/down for network. Re-open it any time with another
left-click.

---

## Metrics reference

### CPU

- **Source:** Windows performance counter `Processor\% Processor Time\_Total`
- **Units:** percent, 0–100
- **Notes:** Requires two samples to produce a nonzero value. The first sample is
  discarded at startup, so CPU reads 0% for the first ~1.5 seconds after launch.
- **Meaning:** Average utilization across all logical cores, identical to the CPU
  percentage shown in Task Manager's Performance tab.

### Memory (RAM)

- **Source:** `GetPerformanceInfo` (psapi.dll) — physical memory, not commit charge
- **Units:** percent (used / total), plus GB used, GB total, GB free
- **Formula:** `used = PhysicalTotal − PhysicalAvailable`; `percent = used / total × 100`
- **Meaning:** Physical RAM in use by the OS and all processes, including file cache
  that Windows can evict on demand. Because Windows treats standby/cache as
  "available," this number may be **lower** than the headline figure shown in some
  third-party tools that count cached bytes as "used."
- **Overlay shows:** percent, used GB / total GB, free GB.

### Disk

- **Source:** Three performance counters under `PhysicalDisk\_Total`:
  - `Disk Read Bytes/sec`
  - `Disk Write Bytes/sec`
  - `% Disk Time` (active time)
- **Units:** percent (active time), plus read KB/s, write KB/s, total Mbps
- **Fallback:** If `% Disk Time` reports 0 but throughput is nonzero, MiniTrayMon
  estimates disk % as `min(100, (read + write KB/s) / 10)`. This is approximate.
- **Meaning:** Aggregated across all physical disks in the system. The `%` is
  active time — the fraction of the sample window the disk spent servicing requests.
  On NVMe and SAN storage, the Windows `% Disk Time` counter is known to over-report
  (sometimes pegged at 100%); this is a Windows limitation, not a MiniTrayMon bug.
- **Overlay shows:** percent, read MB/s (+ KB/s), write MB/s (+ KB/s), total Mbps.

### Network

- **Source:** `Network Interface\Bytes Sent/sec` and `Bytes Received/sec`
- **Interface selection:** the first non-loopback, non-`isatap`, non-`tunnel`,
  non-`teredo` adapter, in performance-counter enumeration order.
- **Units:** Mbps (megabits per second), down (↓) and up (↑)
- **Conversion:** `bytes/sec × 8 / 1024 / 1024`
- **Meaning:** Throughput on the selected adapter only. If you have a VPN, virtual
  switch, or Hyper-V/WSL bridge, MiniTrayMon may bind to the wrong adapter — see
  the README troubleshooting section.
- **Overlay shows:** ↓ download Mbps, ↑ upload Mbps.

### Overall %

A weighted blend of the four metrics, intended to approximate "how busy is my PC
right now" as a single number.

```
netLoad  = min(100, (down + up) × 2)     // ~50 Mbps total ≈ saturated
diskLoad = min(100, diskPercent)
overall  = CPU × 0.35 + RAM × 0.35 + diskLoad × 0.15 + netLoad × 0.15
```

| Weight | Metric |
|--------|--------|
| 35%    | CPU    |
| 35%    | RAM    |
| 15%    | Disk   |
| 15%    | Network |

The weighting is a heuristic, not a scientific measurement. RAM and CPU dominate
because they best reflect sustained user-perceived load; disk and network are
weighted lower because brief spikes don't usually make a PC feel "busy."

---

## Behavior notes

- **Single instance.** Only one MiniTrayMon process can run at a time. A second launch
  silently exits. The guard uses a global named mutex
  (`Global\MiniTrayMon_SingleInstance_v1`).
- **Settings file.** `minitraymon.settings` is written next to the executable and
  contains only the display-mode name (e.g. `Bars`). Deleting it resets to the default
  (Bars) on next launch.
- **No admin rights.** MiniTrayMon reads performance counters in read-only mode and
  does not write to the registry or system folders. It runs fine under a standard
  user account.
- **Icon handle cleanup.** Each poll generates a new icon handle; the previous one is
  explicitly destroyed via `user32!DestroyIcon` to prevent GDI handle leaks over long
  uptimes.
- **First-poll warmup.** A one-shot 600 ms warmup timer fires the first poll slightly
  early so the icon shows real data within a second of launch instead of the
  placeholder "M" icon.

---

## Tips

- **Pin the icon to the taskbar corner.** Windows 11 hides new tray icons by default.
  Drag MiniTrayMon out of the overflow area so it's always visible.
- **Use Overall mode for ambient awareness.** One number + one color tells you whether
  to investigate further. Switch to Bars or Numeric when something looks high.
- **The overlay is a glance, not a dock.** It closes after 15 seconds on purpose —
  this is a deliberate footprint-vs-utility tradeoff. If you want a permanent
  dashboard, keep Task Manager open.
- **Re-running `install_startup.bat` is safe.** It overwrites the existing shortcut
  in place; you will not get duplicates.
- **Diagnosing network mis-selection.** If Network reads 0 Mbps while you're clearly
  downloading, your active adapter isn't the one MiniTrayMon bound to. Disable unused
  virtual adapters in `ncpa.cpl`, or hard-code the interface name in
  `StatsCollector.GetFirstNetworkInstance()`.
- **Comparing to Task Manager.** CPU and RAM numbers should match closely. Disk % may
  differ if Windows' `% Disk Time` counter is misbehaving on your storage. Network
  numbers in Task Manager are per-adapter and shown in Mbps — MiniTrayMon shows the
  same bytes/sec figure converted to Mbps.
