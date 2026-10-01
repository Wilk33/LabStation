# Siglent SDM3000 Viewer

Planowana, samodzielna aplikacja Windows oraz panel wielokrotnego użytku do obserwacji multimetru SIGLENT SDM3055 przez LAN/VXI-11. Pierwsze wydanie ma być panelem odczytowym, a nie zdalnym pulpitem sterowania miernikiem.

Domyślny adres urządzenia w tej instalacji: 192.168.200.131.

Szczegółowy, zweryfikowany zakres, podział bezpiecznych poleceń oraz projekt panelu opisuje dokument [Zakres panelu SDM3000](../docs/sdm3000-viewer-scope.md).

## Założenia pierwszego wydania

- wspólny styl LabStation oraz niezależny panel UserControl,
- połączenie LAN/VXI-11 przez Shared/LabStation.Instruments,
- skan sieci i Auto connect zgodne z Oscyloskopem i Generatorem,
- identyfikacja modelu przez "*IDN?",
- odczyt bieżącej wartości przez "DATA:LAST?",
- odczyt liczby danych w pamięci przez "DATA:POINts?",
- pobranie zakończonej serii przez "FETCh?" bez usuwania jej z pamięci,
- odczyt funkcji, zakresu i dostępnej konfiguracji bez ich zmiany,
- lokalny wykres trendu, lokalne statystyki oraz eksport CSV,
- odczyt statystyk i histogramu wyliczanych przez miernik, jeśli są aktywne,
- jawne przedstawienie przeciążenia lub otwartego obwodu zamiast liczby około 9,9E37.

## Granica bezpieczeństwa

Pierwsze wydanie nie wysyła poleceń konfigurujących funkcję, zakres, wyzwalanie, limity ani sieć. Nie używa też zapytań, które mimo znaku zapytania uruchamiają pomiar lub usuwają dane, takich jak "MEASure...?", "READ?", "R?" i "DATA:REMove?".

Polecenia zmieniające stan mogą zostać dodane później jako osobny, jawny tryb po testach na fizycznym SDM3055 i osobnym zatwierdzeniu zakresu.

## Dostępne zasoby

- Siglent_SDM3055.ico - ikona przyszłego EXE,
- Siglent_SDM3055.png - grafika referencyjna,
- wspólne transporty VXI-11 i TCP SCPI,
- wspólny skaner sieci,
- wspólny mechanizm serializacji operacji,
- wspólny wykres TimeSeriesPlot,
- wspólny motyw, menu, kontrolki i okna informacji.
