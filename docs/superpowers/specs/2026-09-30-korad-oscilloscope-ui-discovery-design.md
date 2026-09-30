# Korad and Oscilloscope UI and discovery design

## Goal

Finish the current Korad chart polish and extend the Oscilloscope with the same cursor-button language, local-network discovery, optional automatic connection and channel-aware CSV menus without adding permanent instructional UI.

## Shared UI

`LabStation.UI` owns two reusable keyed styles: the existing general instrument button geometry and a cursor-button style with the Korad dimensions, black text and application-supplied cursor color. Both Korad and Oscilloscope use that cursor style. Menu separators use a separate theme-aware separator brush and begin after the check-mark gutter, matching the submenu layout rather than drawing through the entire popup.

The shared plot keeps its existing rendering logic. Korad's compact `CurrentChart` reserves at least 58 px below the plot rectangle because horizontal labels are rendered at `area.Bottom+8`, while cursor readout and the `Czas` title are rendered at `area.Bottom+32`. This fixes clipping at the source instead of enlarging the window again.

## Network discovery

`LabStation.Instruments` provides a transport-independent scanner contract and a VXI-11 identity probe. Local IPv4 candidates are derived from active interfaces. A subnet larger than `/24` is restricted to the local `/24` segment so discovery remains bounded; network, broadcast, loopback, APIPA and the local address are excluded. Probes run off the UI thread with bounded concurrency, short connect/I/O timeouts and per-address failure isolation.

The scanner returns an address plus parsed `ScpiIdentity`. It never decides which product is supported. `ScopeClient` owns the exact SIGLENT SDS1102CML+ model predicate.

## Oscilloscope behavior

The main menu contains `Narzędzia` with `Skanuj sieć`, a separator and a checkable `Auto connect` item. Scanning changes only the IP field when a supported oscilloscope is found. If Auto connect is checked, the panel then connects through the normal connection path. With an empty saved address startup automation does nothing. Automatic connection failures are silent and leave the application Offline; manual connection retains the existing warning.

Auto connect and the address are persisted together. The reusable panel receives scanner, transport factory and settings store through an injectable constructor, while its parameterless constructor supplies production implementations.

The old bottom `Zapisz CSV` button is removed. A top-level `Zapisz jako` menu exposes `CH1 CSV`, `CH2 CSV` and `CH1 i CH2 CSV`. Availability follows the channels contained in the last manual capture. Each export writes only the requested captured channel or channels and keeps the existing atomic temporary-file replacement.

General Oscilloscope action buttons use Korad-sized widths. Its four cursor buttons use the shared Korad cursor style while preserving existing cursor activation, selection and movement behavior.

## Verification and publication

All new behavior is developed test-first. Verification covers subnet boundaries, probe failure isolation, model filtering, Auto connect, menu structure, channel-specific availability, cursor geometry/colors, Korad plot footer bounds and both applications' full suites. Korad becomes v0.2.5 and Oscilloscope v0.7.0. Both are published to their stable `artifacts/final/win-x64` directories with versioned ZIP archives. No physical network scan or hardware command is performed during automated verification.
