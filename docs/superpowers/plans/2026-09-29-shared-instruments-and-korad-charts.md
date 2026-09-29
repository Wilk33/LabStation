# Shared Instruments and Korad Charts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wydzielić wspólną komunikację sieciową i wykres, przenieść Generator do LabStation oraz rozszerzyć Korada o zoom, kursory i poprawny tryb symetryczny.

**Architecture:** Biblioteka `LabStation.Instruments` zawiera transporty, SCPI i neutralne prymitywy kolejek, a biblioteka `LabStation.UI` neutralny wykres szeregów czasowych. Klienci urządzeń, parsery i bezpieczeństwo pozostają w aplikacjach.

**Tech Stack:** .NET 10, C# 14, WPF, ONC RPC/VXI-11, SCPI, xUnit i testowe aplikacje konsolowe.

**Spec:** `docs/superpowers/specs/2026-09-29-shared-instruments-and-korad-charts-design.md`

## Global Constraints

- Brak stałych komentarzy lub podpowiedzi w UI.
- Brak trybów demonstracyjnych.
- Każda aplikacja pozostaje osobnym EXE i panelem możliwym do ponownego użycia.
- Brak fizycznych zapisów do urządzeń w automatycznej weryfikacji.
- Korad zachowuje własne reguły COM i bezpieczeństwa OUT0.
- Maksymalna historia Korada wynosi 20 480 próbek.
- Port 1 jest dodatnią, a Port 2 ujemną stroną trybu symetrycznego.

## Review Focus

- Wieloczęściowa odpowiedź RPC i zapis większy od `maxRecvSize` nie mogą zostać ucięte.
- Zamknięcie sesji podczas oczekujących poleceń nie może pozostawić niezakończonych zadań.
- Zoom i kursory muszą pozostać poprawne po usunięciu najstarszych próbek.
- Przejście z trybu symetrycznego do innego nie może mieszać szeregów o różnych znaczeniach.
- Tryb symetryczny nie może odwrócić przypisania Port 1 dodatni, Port 2 ujemny.

---

### Task 1: Wspólna biblioteka przyrządów

**Files:**
- Create: `Shared/LabStation.Instruments/**`
- Create: `Shared/LabStation.Instruments.Tests/**`

**Interfaces:**
- Produces: `IInstrumentTransport`, `Vxi11Transport`, `TcpScpiTransport`, `ScpiConnection`, `ScpiIdentity`, `SerializedOperationGate`, `LatestRequestQueue<T>`.

- [ ] Dodać testy zachowania transportów, parsera i kolejek.
- [ ] Uruchomić testy i potwierdzić RED z powodu brakujących interfejsów.
- [ ] Dodać minimalne implementacje.
- [ ] Uruchomić testy i potwierdzić GREEN.

### Task 2: Migracja Oscyloskopu i Generatora

**Files:**
- Modify: `SDS1000CML Viewer/src/Scope.Core/**`
- Create: `SDG1000X Control/**`
- Modify: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: API `LabStation.Instruments` z Task 1.
- Produces: dwa niezależne EXE używające jednej implementacji VXI-11.

- [ ] Najpierw dodać testy architektury i przeniesiony zestaw testów Generatora.
- [ ] Potwierdzić RED przed dodaniem lub podpięciem kodu.
- [ ] Usunąć lokalne duplikaty transportu i kolejki, zachowując lokalne sesje urządzeń.
- [ ] Podpiąć Generator do `LabStation.UI` i usunąć tryb demonstracyjny.
- [ ] Potwierdzić GREEN obu pełnych zestawów testów.

### Task 3: Wspólny wykres i Korad

**Files:**
- Create: `Shared/LabStation.UI/Controls/TimeSeriesPlot.cs`
- Modify: `SDS1000CML Viewer/src/Scope.App/WavePlot.cs`
- Modify: `KA3005P/src/Ka3005P.App/Controls/CurrentChart.cs`
- Modify: `KA3005P/src/Ka3005P.App/ViewModels/Chart*.cs`
- Modify: `KA3005P/src/Ka3005P.App/Views/ChartWindow.*`

**Interfaces:**
- Produces: współdzielony wykres, cztery kursory Oscyloskopu i dwa kursory Korada dostępne tylko w OFF.

- [ ] Dodać testy 20 480 punktów, zoomu, kursorów OFF i wysokości 360 px.
- [ ] Potwierdzić RED.
- [ ] Dodać współdzielony wykres i cienkie adaptery aplikacji.
- [ ] Potwierdzić GREEN wraz z testem WPF Oscyloskopu.

### Task 4: Symetryczny Dual i główne UI

**Files:**
- Modify: `KA3005P/src/Ka3005P.Core/Dual/DualMeasurement.cs`
- Modify: `KA3005P/src/Ka3005P.App/ViewModels/DualSupplyViewModel.cs`
- Modify: `KA3005P/src/Ka3005P.App/Views/DualSupplyView.xaml`
- Modify: `KA3005P/tests/Ka3005P.Tests/**`

**Interfaces:**
- Produces: Port 1 dodatni, Port 2 ujemny, oddzielne dane główne i szeregi wykresu.

- [ ] Dodać testy znaków, danych głównych i szeregów.
- [ ] Potwierdzić RED.
- [ ] Wdrożyć minimalne zmiany modelu, view modelu i XAML.
- [ ] Potwierdzić GREEN całego Korada.

### Task 5: Wersje, ikony, pakiety, dokumentacja i Git

**Files:**
- Modify: wersje, README, changelogi i notatki AI-Context.
- Track: dostarczone ikony SDM3055 i SDL1020X-E.

**Interfaces:**
- Consumes: kompletne aplikacje z Task 2-4.
- Produces: zweryfikowane lokalne EXE/ZIP i publiczną historię źródłową Git.

- [ ] Podnieść wersje i uzupełnić dokumentację bez dodawania stałych objaśnień do UI.
- [ ] Uruchomić wszystkie testy i kompilacje Release.
- [ ] Opublikować trzy samodzielne paczki lokalne i wykonać smoke test bez sprzętu.
- [ ] Zaktualizować Obsidian wyłącznie potwierdzonymi wynikami.
- [ ] Zacommitować i wypchnąć źródła, następnie potwierdzić CI.

