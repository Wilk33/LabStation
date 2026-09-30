# Korad and Oscilloscope UI and discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish Korad chart layout and add shared cursor styling, VXI-11 discovery, Auto connect and channel-aware CSV menus to the Oscilloscope.

**Architecture:** `LabStation.UI` owns appearance, `LabStation.Instruments` owns discovery mechanics, and `Scope.Core` owns model support. The Oscilloscope panel composes these services through injected interfaces and keeps device/session behavior local.

**Tech Stack:** C# 14, .NET 10, WPF, custom console test harnesses, xUnit for Korad.

**Spec:** `docs/superpowers/specs/2026-09-30-korad-oscilloscope-ui-discovery-design.md`

## Global Constraints

- Do not add permanent instructional text to either UI.
- Do not scan the real local network or connect to physical hardware during automated verification.
- Preserve existing tooltips, waveform behavior, cursor mechanics and safe session shutdown.
- Publish unpacked latest builds only to each application's stable `artifacts/final/win-x64` directory.

## Review Focus

- A `/16` local interface must not cause an unbounded 65,534-host scan; only its local `/24` segment is considered.
- A timeout or malformed `*IDN?` response from one host must not abort discovery of another supported host.
- Auto connect with an empty address must perform no connection attempt.
- CSV menu availability must distinguish CH1-only, CH2-only and two-channel captures.
- Korad footer labels and `Czas` must remain inside the chart control at the minimum window size.

---

### Task 1: Shared cursor style, separator and Korad footer

**Files:**
- Modify: `Shared/LabStation.UI/Themes/LabStationTheme.xaml`
- Modify: `Shared/LabStation.UI/SystemTheme.cs`
- Modify: `KA3005P/src/Ka3005P.App/Controls/CurrentChart.cs`
- Modify: `KA3005P/src/Ka3005P.App/Views/ChartWindow.xaml`
- Test: `KA3005P/tests/Ka3005P.Tests/ViewModels/ChartViewModelTests.cs`

**Interfaces:**
- Produces: keyed style `LabStationCursorButtonStyle`; dynamic resource `SystemChromeSeparatorBrush`.
- Consumes: existing `LabStationButtonBaseStyle` and `TimeSeriesPlot.PlotBounds`.

- [ ] Write tests asserting Korad cursor buttons are 76 px wide with black text and share the reusable style, and that the plot reserves at least 58 px below `PlotBounds`.
- [ ] Run the focused Korad tests and confirm RED.
- [ ] Add the shared style, themed separator layout and 58 px Korad footer; apply the style to both Korad cursor buttons.
- [ ] Run focused and full Korad tests and confirm GREEN.
- [ ] Commit the task.

### Task 2: Shared local-network instrument discovery

**Files:**
- Create: `Shared/LabStation.Instruments/Discovery/IInstrumentNetworkScanner.cs`
- Create: `Shared/LabStation.Instruments/Discovery/Ipv4Subnet.cs`
- Create: `Shared/LabStation.Instruments/Discovery/InstrumentNetworkScanner.cs`
- Create: `Shared/LabStation.Instruments/Discovery/Vxi11IdentityProbe.cs`
- Test: `Shared/LabStation.Instruments.Tests/Program.cs`

**Interfaces:**
- Produces: `DiscoveredInstrument`, `IInstrumentNetworkScanner.FindFirstAsync(Func<ScpiIdentity,bool>,CancellationToken)`, `IInstrumentIdentityProbe.IdentifyAsync(string,CancellationToken)` and bounded IPv4 candidate generation.
- Consumes: `ScpiIdentity`, `ScpiConnection`, `Vxi11Transport` and `Vxi11Options`.

- [ ] Write tests for `/24`, large-subnet bounding, failed-host isolation, unsupported identity rejection and supported identity selection.
- [ ] Run shared-instrument tests and confirm RED for missing discovery types.
- [ ] Implement the minimal subnet, scanner and VXI-11 probe types.
- [ ] Run shared-instrument and solution regression tests and confirm GREEN.
- [ ] Commit the task.

### Task 3: Oscilloscope menus, Auto connect, CSV selection and button geometry

**Files:**
- Create: `SDS1000CML Viewer/src/Scope.App/OscilloscopeSettings.cs`
- Modify: `SDS1000CML Viewer/src/Scope.Core/ScopeClient.cs`
- Modify: `SDS1000CML Viewer/src/Scope.App/MainWindow.xaml`
- Modify: `SDS1000CML Viewer/src/Scope.App/MainWindow.xaml.cs`
- Modify: `SDS1000CML Viewer/src/Scope.App/OscilloscopeView.xaml`
- Modify: `SDS1000CML Viewer/src/Scope.App/OscilloscopeView.xaml.cs`
- Test: `SDS1000CML Viewer/tests/Scope.Tests/Program.cs`
- Test: `SDS1000CML Viewer/tests/Scope.UiTests/Program.cs`

**Interfaces:**
- Consumes: Task 1 `LabStationCursorButtonStyle`; Task 2 `IInstrumentNetworkScanner`; `ScopeClient.IsSupported(ScpiIdentity)`.
- Produces: panel properties/events used by the standalone shell for scan, Auto connect and CSV menu availability.

- [ ] Write core tests for the supported-model predicate and UI tests for the two menus, absent Save button, cursor geometry, compact action buttons and channel-dependent CSV enablement.
- [ ] Write a controlled fake-scanner UI test proving scan fills the IP and Auto connect uses the discovered host; prove empty startup address performs no connection.
- [ ] Run Scope tests and UI tests and confirm RED.
- [ ] Implement settings injection, startup automation, network scan composition, menu handlers, channel-filtered save and shared cursor styles without adding permanent instructions.
- [ ] Run Scope tests, UI tests and shared regressions and confirm GREEN.
- [ ] Commit the task.

### Task 4: Versions, documentation, packages and release evidence

**Files:**
- Modify: `KA3005P/src/Ka3005P.App/Ka3005P.App.csproj`
- Modify: `SDS1000CML Viewer/Directory.Build.props`
- Modify: `README.md`
- Modify: `KA3005P/README.md`
- Modify: `SDS1000CML Viewer/README.md`
- Modify: relevant application version tests and Obsidian project notes after verification.

**Interfaces:**
- Produces: Korad v0.2.5 and Oscilloscope v0.7.0 stable win-x64 packages.
- Consumes: all earlier tasks and existing build scripts.

- [ ] Change version expectations first and confirm RED.
- [ ] Update versions and documentation, then confirm version tests GREEN.
- [ ] Run both full build scripts with `-Publish`, launch both final EXEs without hardware, verify ZIP-contained EXE hashes, and retain only stable unpacked directories.
- [ ] Perform final whole-diff review, fix Important findings test-first, and rerun every affected suite.
- [ ] Commit, push `main`, wait for GitHub Actions success, rebuild from final commit, record hashes and update Obsidian notes.
