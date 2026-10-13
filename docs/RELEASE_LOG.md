# MiniTrayMon — Release Log

## v1.6.0 — July 23, 2026 — REVERTED

Attempted to replace colorkey transparency with true per-pixel alpha
(`UpdateLayeredWindow`) for both the tray icon and the taskbar meter, to
fix a visible "mismatched background box" issue. The underlying diagnosis
was correct (colorkey transparency requires an exact single-color match,
and Windows 11's acrylic taskbar background genuinely varies by 15-20 RGB
points across its width) and ground-truth pixel sampling confirmed the
alpha-blended corners matched the taskbar almost exactly. However, the
user reported the actual visual result looked worse than the v1.5.0
colorkey approach in practice, so this release was reverted in full.
`IconAlphaFix.cs` and `LayeredWindowHelper.cs` were removed; both the
tray icon (`IconRenderer.Render`) and the taskbar meter
(`TaskbarSpeedMeter`) are back to the exact v1.5.0 implementation
(`Bitmap.GetHicon()` for the icon, `SetLayeredWindowAttributes` +
`LWA_COLORKEY` for the meter). If the black/mismatched-background issue
resurfaces, revisit with a different approach (e.g. sampling the
taskbar color at the meter's actual screen position instead of a single
mid-screen point, or re-sampling periodically instead of once at
construction).

---

## v1.5.0 — July 22, 2026

### Added
- **Size scaling (4 levels)**: New "Size" submenu in both the tray icon
  menu and the meter's own right-click menu, with Increase / Decrease

  shortcuts and explicit picks: Small (0.75×), Medium (1.0×, default),
  Large (1.3×), Extra Large (1.6×). Affects the tray icon fonts/bars
  AND the taskbar speed meter fonts/dimensions together as one shared
  setting — size feels like a single knob, not two.
  - Auto-shrink safety: at larger scales with 3-4 metrics enabled, the
    number/percentage font auto-shrinks to fit within each cell/bar width
    so text never overflows into neighboring segments. Verified via
    ground-truth pixel measurement (not just vision).
- **Start with Windows (auto-start toggle)**: New "Start with Windows"
  checkbox in the tray icon menu. Writes/removes a per-user registry
  entry under `HKCU\...\Run` (no admin rights needed). Reads the real
  current registry state on startup, so the checkbox never lies if the
  entry was removed externally (e.g. via Task Manager's Startup tab).
  If the registry write fails, the user is warned rather than the
  checkbox silently reverting.
- **Unified settings profile**: All display preferences now save and
  restore together as one coherent "look" profile:
  - DisplayMode (NumericRow / VerticalBars)
  - VisibleMetrics (which of CPU/RAM/Disk/Net to show)
  - MeterMode (Float / Fixed / Overlay)
  - UiScale (Small / Medium / Large / ExtraLarge)
  - MeterX, MeterY (dragged position)
  - SpeedMeterVisible (whether the taskbar meter shows)
  On launch, the app restores all of these and reappears exactly as it
  was left — same look, same position, same metrics, same size.

### Changed
- Bumped version to 1.5.0.0 (assembly + About dialog).
- `IconRenderer.Render()` signature now takes a `UiScale` parameter.
- `TaskbarSpeedMeter` fonts are now mutable (rebuilt on scale change)
  with proper disposal of the previous fonts to avoid GDI leaks.

### Verified
- Process health: 53.1 MB RAM, 531 handles (stable across 12s)
- GDI handle leak check: 531 → 531 → 531 → 531 (no leak from
  scale-change font rebuilds or the 500ms topmost timer)
- Size scaling: 4 preview PNGs generated (Small/Medium/Large/ExtraLarge),
  vision-confirmed visible size difference, pixel-measurement-confirmed
  no text overflow at any scale/metric-count combination
- Auto-start: standalone test of `Properties.SetAutoStartEnabled()`
  confirmed enable→True→disable→False cycle works against the real
  Windows registry; registry correctly cleaned up after disable
- Unified profile persistence: set a distinctive profile (VerticalBars,
  CPU+RAM only, Fixed mode, Large scale, custom position 800,910),
  restarted the app, and verified the meter restored to exact expected
  size (286×54) and exact saved position (800,910)
- Z-order regression: 4/4 stays on top after taskbar clicks (no
  regression from the v1.2.1 fix)
- Live stats: CPU/RAM/Disk/Net values updating every 1.5s poll

---

## v1.4.0 — July 22, 2026

### Added
- **Draggable + right-clickable taskbar meter**: Replaced the single
  locked/unlocked toggle with three explicit interaction modes:
  - Float (default): fully interactive — left-click drag to reposition,
    right-click for its own context menu
  - Fixed: position locked but still clickable for the right-click menu
  - Overlay: click-through (old "locked" behavior) — mouse passes to the
    taskbar beneath; only reachable back via the tray icon's menu
- **Meter's own right-click menu**: Position mode submenu, Show metrics
  submenu (toggle CPU/RAM/Disk/Network right there), Reset position,
  Hide speed meter. All wired back to the tray app so both menus stay
  in sync.
- `MeterMode` enum, persisted via `Properties.GetMeterMode()`/`SetMeterMode()`.

### Fixed
- **"Can't click or drag the meter" bug**: The meter defaulted to
  click-through (`WS_EX_TRANSPARENT`), making it invisible to all mouse
  input. Now defaults to Float (clickable/draggable).

---

## v1.3.0 — July 22, 2026

### Changed
- **Enlarged the taskbar speed meter**: 340×30 → 440×42 (30% wider, 40%
  taller). Value font 9pt → 12pt bold. Label font 7.5pt → 9.5pt.
  Per-segment width 85px → 110px.

---

## v1.2.1 — July 22, 2026

### Fixed
- **Meter disappears behind taskbar on click**: Windows' shell
  (explorer.exe) re-asserts its own topmost z-order whenever the taskbar
  is clicked, pushing our overlay behind it. Added a 500ms recurring
  `SetWindowPos(HWND_TOPMOST, ...)` timer plus immediate re-assertion
  on show/visibility-change to re-pin above the shell.

---

## v1.2.0 — July 22, 2026

### Added
- **Draggable/lockable meter position**: Right-click tray icon →
  "Lock speed meter position" to toggle. Unlocked = draggable; locked =
  click-through. Position saved/restored across restarts.
- **Per-metric show/hide**: Right-click tray icon → "Show metrics" →
  check/uncheck CPU, RAM, Disk, Network independently. Selecting only
  CPU makes both the tray icon and taskbar meter show just CPU.
- `MetricFlags` enum (bitwise flags) for per-metric visibility.

---

## v1.1.0 — July 22, 2026

### Changed
- **128×128 transparent tray icon** (was 64×64 solid dark background)
- **Outlined text** on all numbers/labels (dark outline + light fill)
  so readable on both light and dark taskbars
- **2 display modes** (replaced old 3): NumericRow and VerticalBars
- **Taskbar speed meter**: slim clock-row-style strip showing
  CPU/RAM/DSK/NET live values, transparent background, auto-detected
  taskbar color for seamless blend

---

## v1.0.0 — July 19, 2026

### Initial release
- Lightweight Windows system tray monitor for CPU, RAM, Disk, Network
- Single self-contained .NET Framework 4 exe (~30 KB)
- ~50 MB RAM footprint (vs Task Manager ~200-300 MB)
- PerformanceCounter-based metrics matching Task Manager accuracy
- Single-instance mutex guard
- Left-click overlay window with detailed stats
- Right-click context menu with live readouts
- Auto-start on Windows boot via startup shortcut
