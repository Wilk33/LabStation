# Siglent SDM3000 Control v0.2.3

Samodzielna aplikacja Windows oraz panel wielokrotnego użytku do ciągłego wykonywania i prezentowania pomiarów multimetru SIGLENT SDM3055 przez LAN/VXI-11. Wydanie 0.2.3 utrzymuje jedną sesję zdalną, inicjuje pomiary poleceniem `READ?`, pozwala wybrać podstawową funkcję pomiarową z aplikacji i mieści pełny główny odczyt w minimalnym poziomym oknie.

Domyślny adres urządzenia w tej instalacji: `192.168.200.131`.

Szczegółowy zakres i wyniki testów sprzętowych opisuje dokument [Zakres panelu SDM3000](../docs/sdm3000-viewer-scope.md).

## Funkcje 0.2.3

- wspólny styl LabStation oraz niezależny panel `UserControl`,
- jedna trwała sesja LAN/VXI-11 przez `Shared/LabStation.Instruments`,
- skan sieci i zapamiętywana opcja `Auto connect`,
- identyfikacja modelu przez `*IDN?`,
- odczyt funkcji i zakresu przez `CONFigure?`,
- ciągłe inicjowanie świeżych pomiarów przez `READ?` co około 100 ms,
- odczyt liczby danych w pamięci przez `DATA:POINts?`,
- osiem przycisków wyboru funkcji: `V AC`, `V DC`, `A AC`, `A DC`, `Ω` oraz wektorowe symbole pojemności, diody i ciągłości,
- obsługa napięcia i prądu AC/DC, rezystancji 2W/4W, pojemności, diody, ciągłości, częstotliwości, okresu i temperatury,
- duży bieżący wynik bez powtarzania obok niego skrótu funkcji, który pozostaje widoczny w nagłówku pomiaru,
- lokalne minimum, maksimum, średnia, peak-to-peak serii, odchylenie standardowe i licznik próbek,
- zapis zebranych odczytów do pliku CSV,
- serializacja konfiguracji i odczytów w obrębie jednej sesji, bez ponownego łączenia w każdym cyklu,
- jawne przedstawienie przeciążenia lub otwartego obwodu zamiast liczby około 9,9E37.

Peak-to-peak w tym panelu jest różnicą maksimum i minimum kolejnych odczytów multimetru. Nie jest to oscyloskopowe Vpp przebiegu wejściowego.

## Interfejs

- stałe, poziome okno 520 x 340 px z aktywną minimalizacją i bez możliwości zmiany rozmiaru,
- pole IP o szerokości 150 px i standardowy przycisk Offline/Online,
- wspólne menu `Narzędzia` z `Skanuj sieć` i `Auto connect`,
- menu `Zapisz jako` z eksportem CSV,
- wspólne menu `O aplikacji` z oknami Autor i Licencja,
- osiem kompaktowych przycisków funkcji pomiarowej w dwóch rzędach,
- powiększony symbol rezystancji oraz wektorowy symbol ciągłości złożony z kropki i dwóch łuków,
- nagłówek bieżącego pomiaru używa symboli pojemności, diody i ciągłości zamiast skrótów tekstowych,
- biały zwykły status i czerwony status błędu.

## Skutki polecenia READ

`READ?` inicjuje pomiar, czeka na jego zakończenie i zwraca świeży wynik. Na sprawdzonym SDM3055 z firmware `1.02.01.29R1` samo `DATA:LAST?` zwracało starą wartość, ponieważ urządzenie po wejściu w Remote przechodziło z Auto trig do Stopped.

Zastosowanie `READ?` ma dwa jawne skutki:

- aplikacja jest kontrolerem pomiaru, a nie pasywnym viewerem,
- pamięć odczytów w urządzeniu jest czyszczona przez rozpoczęcie nowej sekwencji pomiarowej.

Historia i statystyki aplikacji są przechowywane lokalnie w trakcie sesji i mogą zostać zapisane do CSV. Przyciski funkcji wysyłają odpowiednie polecenie `CONFigure`. Aplikacja nie zmienia zakresu, NPLC, filtrów, limitów, ustawień sieci ani konfiguracji wyzwalania.

Transport pozostaje otwarty przez całą sesję i wysyła `device_local` dopiero przy końcowym rozłączeniu. Test fizycznego urządzenia wykazał jednak, że firmware może pozostawić panel w stanie Remote aż do naciśnięcia Shift na urządzeniu. Aplikacja nie obiecuje programowego wyjścia z tego stanu.

## Budowanie

```powershell
& '.\build.ps1'
& '.\build.ps1' -Publish
```

Publikacja samodzielna dla Windows x64 trafia do `artifacts/final/win-x64`. W tym katalogu znajduje się `Siglent.SDM3000.Control.exe` wraz z licencją, informacjami o zależnościach i README.

## Status weryfikacji

Wersja 0.2.3 przeszła 14/14 testów automatycznych, obejmujących mapowanie ośmiu poleceń `CONFigure`, zachowanie jednej sesji, kolejne `READ?`, serializację operacji oraz kompaktowy układ WPF z nieprzyciętym głównym odczytem, minimalizacją i wektorowymi symbolami pojemności, diody i ciągłości w przyciskach oraz nagłówku pomiaru.

Na fizycznym SDM3055 z firmware `1.02.01.29R1` jedna sesja zwróciła trzy kolejne świeże pomiary `V DC`: 11,30298 mV, 11,30100 mV i 11,30511 mV. Test nie zmieniał funkcji pomiarowej. Przyciski funkcji zostały zweryfikowane programowo, ale ich działanie na fizycznym mierniku pozostaje do ręcznego sprawdzenia podczas użytkowania aplikacji.


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
