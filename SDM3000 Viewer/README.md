# Siglent SDM3000 Control

Samodzielna aplikacja Windows oraz panel wielokrotnego użytku do wykonywania i prezentowania pomiarów multimetru SIGLENT SDM3055 przez LAN/VXI-11. Wydanie 0.1.2 nie przełącza funkcji ani zakresu, ale aktywnie inicjuje pojedynczy pomiar poleceniem `READ?`.

Domyślny adres urządzenia w tej instalacji: `192.168.200.131`.

Szczegółowy zakres i wyniki testów sprzętowych opisuje dokument [Zakres panelu SDM3000](../docs/sdm3000-viewer-scope.md).

## Funkcje 0.1.2

- wspólny styl LabStation oraz niezależny panel `UserControl`,
- krótkie połączenia LAN/VXI-11 przez `Shared/LabStation.Instruments`,
- skan sieci i zapamiętywana opcja `Auto connect`,
- identyfikacja modelu przez `*IDN?`,
- odczyt funkcji i zakresu przez `CONFigure?`,
- inicjowanie świeżego pomiaru przez `READ?`,
- odczyt liczby danych w pamięci przez `DATA:POINts?`,
- automatyczne rozpoznawanie funkcji i zakresu bez przełączników trybu pomiarowego w aplikacji,
- obsługa napięcia i prądu AC/DC, rezystancji 2W/4W, pojemności, diody, ciągłości, częstotliwości, okresu i temperatury,
- duży bieżący wynik z nazwą właściwą dla funkcji, między innymi `Vrms`, `Irms`, `Vdc`, `Idc`, `R`, `C` i `Vf`,
- lokalne minimum, maksimum, średnia, peak-to-peak serii, odchylenie standardowe i licznik próbek,
- zapis zebranych odczytów do pliku CSV,
- zamykanie transportu po identyfikacji i po każdym cyklu pomiarowym,
- jawne przedstawienie przeciążenia lub otwartego obwodu zamiast liczby około 9,9E37.

Peak-to-peak w tym panelu jest różnicą maksimum i minimum kolejnych odczytów multimetru. Nie jest to oscyloskopowe Vpp przebiegu wejściowego.

## Interfejs

- stałe, poziome okno 920 x 280 px z aktywną minimalizacją,
- pole IP o szerokości 150 px i standardowy przycisk Offline/Online,
- wspólne menu `Narzędzia` z `Skanuj sieć` i `Auto connect`,
- menu `Zapisz jako` z eksportem CSV,
- wspólne menu `O aplikacji` z oknami Autor i Licencja,
- brak wyboru trybu pomiarowego - funkcja jest odczytywana z fizycznego miernika,
- biały zwykły status i czerwony status błędu.

## Skutki polecenia READ

`READ?` inicjuje pomiar, czeka na jego zakończenie i zwraca świeży wynik. Na sprawdzonym SDM3055 z firmware `1.02.01.29R1` samo `DATA:LAST?` zwracało starą wartość, ponieważ urządzenie po wejściu w Remote przechodziło z Auto trig do Stopped.

Zastosowanie `READ?` ma dwa jawne skutki:

- aplikacja jest kontrolerem pomiaru, a nie pasywnym viewerem,
- pamięć odczytów w urządzeniu jest czyszczona przez rozpoczęcie nowej sekwencji pomiarowej.

Historia i statystyki aplikacji są przechowywane lokalnie w trakcie sesji i mogą zostać zapisane do CSV. Aplikacja nadal nie zmienia funkcji, zakresu, NPLC, filtrów, limitów, ustawień sieci ani konfiguracji wyzwalania.

Transport wysyła `device_local` i zamyka połączenie po każdym cyklu. Test fizycznego urządzenia wykazał jednak, że firmware może pozostawić panel w stanie Remote aż do naciśnięcia Shift na urządzeniu. Aplikacja nie obiecuje programowego wyjścia z tego stanu.

## Budowanie

```powershell
& '.\build.ps1'
& '.\build.ps1' -Publish
```

Publikacja samodzielna dla Windows x64 trafia do `artifacts/final/win-x64`. W tym katalogu znajduje się `Siglent.SDM3000.Control.exe` wraz z licencją, informacjami o zależnościach i README.

## Zasoby

- `Siglent_SDM3055.ico` - wielorozmiarowa ikona EXE 16, 32 i 256 px,
- `Siglent_SDM3055.png` - grafika referencyjna,
- wspólne transporty VXI-11 i TCP SCPI,
- wspólny skaner sieci,
- wspólny mechanizm serializacji operacji,
- wspólny motyw, menu, kontrolki i okna informacji.

## Oficjalne źródła

- SIGLENT SDM Series Programming Guide EN02A: https://siglentna.com/wp-content/uploads/dlm_uploads/2017/10/SDM-Series-Digital-Multimeter_ProgrammingGuide_EN02A.pdf
- SIGLENT SDM3055 User Manual EN03B: https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2023/10/SDM3055_UserManual-EN03B.pdf
- SIGLENT firmware dla multimetrów: https://www.siglenteu.com/service-and-support/firmware-software/digital-multimeters/
