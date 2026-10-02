# Siglent SDM3000 Viewer

Samodzielna aplikacja Windows oraz panel wielokrotnego użytku do obserwacji multimetru SIGLENT SDM3055 przez LAN/VXI-11. Wydanie 0.1.0 jest panelem odczytowym, a nie zdalnym pulpitem sterowania miernikiem.

Domyślny adres urządzenia w tej instalacji: `192.168.200.131`.

Szczegółowy, zweryfikowany zakres i podział bezpiecznych poleceń opisuje dokument [Zakres panelu SDM3000](../docs/sdm3000-viewer-scope.md).

## Funkcje 0.1.0

- wspólny styl LabStation oraz niezależny panel `UserControl`,
- połączenie LAN/VXI-11 przez `Shared/LabStation.Instruments`,
- skan sieci i zapamiętywana opcja `Auto connect`,
- identyfikacja modelu przez `*IDN?`,
- odczyt bieżącej wartości przez `DATA:LAST?`,
- odczyt liczby danych w pamięci przez `DATA:POINts?`,
- automatyczne rozpoznawanie funkcji i zakresu bez przełączników trybu pomiarowego w aplikacji,
- obsługa napięcia i prądu AC/DC, rezystancji 2W/4W, pojemności, diody, ciągłości, częstotliwości, okresu i temperatury,
- duży bieżący wynik z nazwą właściwą dla funkcji, między innymi `Vrms`, `Irms`, `Vdc`, `Idc`, `R`, `C` i `Vf`,
- lokalne minimum, maksimum, średnia, peak-to-peak serii, odchylenie standardowe i licznik próbek,
- jawne przedstawienie przeciążenia lub otwartego obwodu zamiast liczby około 9,9E37.

Peak-to-peak w tym panelu jest różnicą maksimum i minimum kolejnych odczytów multimetru. Nie jest to oscyloskopowe Vpp przebiegu wejściowego.

## Interfejs

- stałe, poziome okno 1040 x 310 px z aktywną minimalizacją,
- pole IP o szerokości 150 px i standardowy przycisk Offline/Online,
- wspólne menu `Narzędzia` z `Skanuj sieć` i `Auto connect`,
- wspólne menu `O aplikacji` z oknami Autor i Licencja,
- brak wyboru trybu pomiarowego. Funkcja jest zawsze odczytywana z fizycznego miernika,
- biały zwykły status i czerwony status błędu.

## Granica bezpieczeństwa

Cykl podglądu korzysta wyłącznie z `CONFigure?`, `DATA:LAST?` i `DATA:POINts?`. Połączenie rozpoczyna tylko `*IDN?`. Aplikacja nie wysyła poleceń zapisu.

Wydanie 0.1.0 nie używa poleceń konfigurujących funkcję, zakres, wyzwalanie, limity ani sieć. Nie używa też zapytań, które mimo znaku zapytania uruchamiają pomiar lub usuwają dane, takich jak `MEASure...?`, `READ?`, `R?` i `DATA:REMove?`.

## Budowanie

```powershell
& '.\build.ps1'
& '.\build.ps1' -Publish
```

Publikacja samodzielna dla Windows x64 trafia do `artifacts/final/win-x64`. W tym katalogu znajduje się `Siglent.SDM3000.Viewer.exe` wraz z licencją, informacjami o zależnościach i README.

## Zasoby

- `Siglent_SDM3055.ico` - ikona EXE,
- `Siglent_SDM3055.png` - grafika referencyjna,
- wspólne transporty VXI-11 i TCP SCPI,
- wspólny skaner sieci,
- wspólny mechanizm serializacji operacji,
- wspólny motyw, menu, kontrolki i okna informacji.
