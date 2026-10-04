# LabStation Main Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zbudować pełnoekranowy LabStation w wariancie 1 jako osobny EXE osadzający pięć niezależnych paneli urządzeń.

**Architecture:** Nowy projekt `LabStation.App` odwołuje się do istniejących projektów WPF i osadza ich publiczne `UserControl`. Nowy kontroler Korada tworzy jeden Single, dwa niezależne Single albo Dual, korzystając z istniejących ViewModeli, sesji i wspólnego rejestru portów. Główne okno jest wyłącznie powłoką układu i deleguje menu do odpowiednich paneli.

**Tech Stack:** .NET 10, WPF, C#, istniejące `LabStation.UI`, `LabStation.Instruments` oraz projekty urządzeń.

**Spec:** `docs/superpowers/specs/2026-10-04-labstation-main-panel-design.md`

## Global Constraints

- Nie zmieniać protokołów urządzeń ani zachowania samodzielnych aplikacji.
- Zachować stały układ wariantu 1 i niezależny cykl życia każdego panelu.
- Nie dodawać stałych komentarzy ani podpowiedzi do UI.
- Korad zmienia konfigurację tylko przy wyłączonych wyjściach.
- Publikacja końcowa trafia do `artifacts/final/win-x64`.

## Review Focus

- Zamknięcie aplikacji podczas aktywnych połączeń musi zwolnić wszystkie sesje bez blokowania UI.
- Dwa panele Single nie mogą dzierżawić tego samego portu COM.
- Przełączenie konfiguracji Korada przy aktywnym wyjściu musi pozostać zablokowane.
- Każde menu skanowania i Auto connect musi sterować wyłącznie właściwym panelem.
- Układ przy rozdzielczości 1920x1080 nie może ucinać statusów ani najważniejszych kontrolek.

---

### Task 1: Powłoka aplikacji i kontrakt układu

**Files:**
- Create: `src/LabStation.App/LabStation.App.csproj`
- Create: `src/LabStation.App/App.xaml`
- Create: `src/LabStation.App/App.xaml.cs`
- Create: `src/LabStation.App/MainWindow.xaml`
- Create: `src/LabStation.App/MainWindow.xaml.cs`
- Create: `tests/LabStation.App.Tests/LabStation.App.Tests.csproj`
- Create: `tests/LabStation.App.Tests/Program.cs`

**Interfaces:**
- Consumes: publiczne widoki i API poleceń czterech aplikacji sieciowych.
- Produces: pełnoekranowe `MainWindow` oraz testowalny opis układu i menu.

- [ ] Napisać test, który tworzy okno i sprawdza pięć osadzonych paneli, proporcje wariantu 1 oraz menu modułów.
- [ ] Uruchomić test i potwierdzić błąd wynikający z braku projektu lub okna.
- [ ] Dodać minimalny projekt, zasoby, okno i przekazywanie poleceń menu.
- [ ] Uruchomić test oraz kompilację Release.

### Task 2: Trzy konfiguracje Korada

**Files:**
- Create: `src/LabStation.App/Korad/KoradWorkspace.cs`
- Create: `src/LabStation.App/Korad/KoradWorkspaceSettings.cs`
- Create: `src/LabStation.App/Korad/KoradPanel.xaml`
- Create: `src/LabStation.App/Korad/KoradPanel.xaml.cs`
- Create: `KA3005P/src/Ka3005P.App/Views/CompactSingleSupplyView.xaml`
- Create: `KA3005P/src/Ka3005P.App/Views/CompactSingleSupplyView.xaml.cs`
- Create: `KA3005P/src/Ka3005P.App/Views/CompactDualSupplyView.xaml`
- Create: `KA3005P/src/Ka3005P.App/Views/CompactDualSupplyView.xaml.cs`
- Create: `KA3005P/src/Ka3005P.App/Views/EmbeddedChartView.xaml`
- Create: `KA3005P/src/Ka3005P.App/Views/EmbeddedChartView.xaml.cs`

**Interfaces:**
- Produces: `KoradConfiguration`, `CurrentConfiguration`, `CanChangeConfiguration`, `SetConfigurationAsync`, `ExportAsync` i `DisposeAsync`.

- [ ] Napisać test przejść `1 Single`, `2 Single`, `Dual`, blokady aktywnego wyjścia i zapamiętywania portów.
- [ ] Potwierdzić oczekiwane błędy testu przed implementacją.
- [ ] Zaimplementować kontroler konfiguracji i zwarte widoki.
- [ ] Uruchomić nowe oraz wszystkie testy Korada.

### Task 3: Eksport i cykl życia modułów

**Files:**
- Modify: `src/LabStation.App/MainWindow.xaml.cs`
- Modify: `src/LabStation.App/Korad/KoradWorkspace.cs`
- Modify: `tests/LabStation.App.Tests/Program.cs`

**Interfaces:**
- Consumes: istniejące `ScanNetworkAsync`, `AutoConnect`, `SaveCsvAsync`, `ExportCsv` i `DisposeAsync`.
- Produces: niezależne delegowanie menu i uporządkowane zamknięcie całego panelu.

- [ ] Napisać test delegowania menu, nazw eksportu P1/P2 oraz jednokrotnego zwolnienia każdego modułu.
- [ ] Potwierdzić oczekiwany błąd testu.
- [ ] Zaimplementować eksport i zamknięcie.
- [ ] Uruchomić nowe testy oraz zestawy regresyjne pięciu aplikacji.

### Task 4: Publikacja i kontrola wizualna

**Files:**
- Create: `LabStation.slnx`
- Create: `build.ps1`
- Modify: `README.md`

**Interfaces:**
- Produces: `LabStation.exe` i zawartość `artifacts/final/win-x64`.

- [ ] Dodać test publikacji i sprawdzenie artefaktu.
- [ ] Uruchomić test przed utworzeniem skryptu i potwierdzić błąd.
- [ ] Dodać skrypt testów, kompilacji i publikacji.
- [ ] Wykonać pełną kompilację Release i publikację.
- [ ] Wyrenderować okno w 1920x1080, skontrolować układ i poprawić wyłącznie ujawnione problemy.
