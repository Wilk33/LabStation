# Zakres panelu Siglent SDM3000 Viewer

Stan dokumentu: zaimplementowane wydanie 0.1.0 panelu obserwacyjnego dla SDM3055.

## Cel

Pierwsza wersja ma przede wszystkim pokazywać stan i wyniki istniejącego pomiaru. Nie powinna samoczynnie przełączać funkcji, zakresu, wyzwalania ani kasować pamięci odczytów.

To rozróżnienie jest konieczne, ponieważ nie każde polecenie SCPI zakończone znakiem zapytania jest pasywne. Na przykład "MEASure...?" konfiguruje funkcję i wykonuje pomiar, a "READ?" rozpoczyna sekwencję pomiarową.

## Potwierdzone możliwości sprzętu

SDM3055 jest multimetrem 5,5 cyfry, do 240 000 zliczeń i do 150 odczytów na sekundę. Obsługuje LAN oraz VXI-11. Funkcje pomiarowe obejmują:

- napięcie DC i AC True RMS,
- prąd DC i AC True RMS,
- rezystancję dwu- i czteroprzewodową,
- pojemność,
- częstotliwość i okres,
- temperaturę,
- ciągłość,
- test diody.

## Polecenia do pierwszego, odczytowego wydania

### Identyfikacja i połączenie

- "*IDN?" - producent, model, numer seryjny i firmware.
- "SYSTem:COMMunicate:LAN:IPADdress? CURRent" - bieżący adres.
- "SYSTem:COMMunicate:LAN:IPADdress? STATic" - adres statyczny.
- zapytania maski i bramy.

### Bieżący wynik i pamięć

- "DATA:LAST?" - ostatni wynik, dostępny również podczas trwającej serii.
- "DATA:POINts?" - liczba wyników w pamięci.
- "FETCh?" - oczekiwanie na zakończenie aktywnej sekwencji i skopiowanie wyników do bufora wyjściowego bez usuwania ich z pamięci.

"FETCh?" nie powinno być wykonywane w krótkim cyklu podglądu, ponieważ może czekać na zakończenie aktywnej sekwencji. Do bieżącego podglądu właściwe jest "DATA:LAST?".

### Odczyt konfiguracji

- "CONFigure?" - bieżąca funkcja i zakres.
- "[SENSe:]FUNCtion?" - bieżąca funkcja.
- zapytania zakresu i auto-range dla aktywnej funkcji.
- zapytania NPLC, filtra i impedancji tam, gdzie są obsługiwane przez SDM3055.
- zapytania ustawień Null/Relative.
- zapytania temperatury, sensora i jednostki.
- zapytania ustawień wyzwalania i liczby próbek.

Opcje opisane w wspólnej instrukcji jako przeznaczone wyłącznie dla SDM3065X nie mogą być pokazywane jako obsługiwane przez SDM3055.

### Statystyki i histogram

Jeżeli obliczenia są już aktywne w mierniku, panel może odczytać:

- minimum, maksimum, średnią, peak-to-peak, odchylenie standardowe i liczbę próbek,
- stan statystyk,
- gotowy histogram, liczbę pomiarów i liczbę przedziałów,
- dolną i górną granicę histogramu,
- limity i stan funkcji limitów.

Histogram zawiera dodatkowy przedział poniżej dolnej granicy i dodatkowy przedział powyżej górnej granicy. Dla 100 skonfigurowanych przedziałów odpowiedź zawiera 102 liczniki.

## Polecenia wyłączone z pierwszego wydania

Następujące polecenia zmieniają konfigurację, rozpoczynają pomiar, kasują stan albo usuwają dane i nie należą do trybu odczytowego:

- "MEASure...?",
- "READ?",
- "R?",
- "DATA:REMove?",
- "CONFigure:<function>",
- "[SENSe:]FUNCtion <function>",
- "INITiate",
- "ABORt",
- "*TRG",
- "*RST",
- "*CLS",
- "SYSTem:PRESet",
- polecenia Clear,
- ustawienia zakresu, NPLC, filtrów, Null, triggera, próbek i sieci bez znaku zapytania.

Szczególnie "MEASure...?" i "READ?" nie mogą być klasyfikowane jako bezpieczny odczyt tylko dlatego, że zawierają znak zapytania.

## Projekt panelu

### Wiersz połączenia

- "IP:" i pole o tej samej szerokości co w Oscyloskopie i Generatorze,
- przycisk Offline/Online,
- menu Narzędzia z pozycjami Skanuj sieć i Auto connect,
- profil obsługi modelu SDM3055,
- domyślny adres 192.168.200.131.

### Odczyt główny

- duża wartość,
- jednostka,
- funkcja,
- zakres i auto/manual,
- czas ostatniej poprawnej próbki,
- liczba punktów w pamięci,
- stan przeciążenia lub otwartego obwodu,
- Status: z białym tekstem, czerwonym wyłącznie przy błędzie.

### Widok wartości

- poziome okno o małej wysokości,
- duży bieżący wynik z jednostką i nazwą wielkości,
- brak wykresu i brak ręcznego wyboru funkcji,
- funkcja oraz zakres są automatycznie odczytywane z miernika,
- minimum, maksimum, średnia, peak-to-peak i odchylenie standardowe są liczone lokalnie dla kolejnych odczytów bieżącej funkcji.

Lokalne peak-to-peak oznacza różnicę maksimum i minimum serii odczytów. Nie jest oscyloskopowym Vpp sygnału wejściowego.

### Statystyki

- osobne wskazanie danych pochodzących z miernika i statystyk lokalnych,
- minimum, maksimum, średnia, peak-to-peak, odchylenie standardowe i licznik,
- brak automatycznego włączania lub czyszczenia statystyk miernika.

### Histogram

- odczyt gotowych binów miernika,
- zakres, liczba próbek i liczba binów,
- jawne dwa dodatkowe biny skrajne,
- brak automatycznego włączania i czyszczenia histogramu.

### Informacje o konfiguracji

- funkcja,
- zakres i auto-range,
- NPLC lub aperture,
- filtr,
- impedancja wejściowa,
- Null/Relative,
- trigger source, trigger count, delay i sample count,
- sensor i jednostka temperatury.

Wartości są tylko wyświetlane. Kontrolki edycyjne mogą powstać dopiero w późniejszym, jawnie sterującym trybie.

## Funkcje, których nie należy obiecywać bez testu sprzętu

- zdalny odczyt obu pól sprzętowego Dual Display,
- gotowy panelowy Trend Chart,
- gotowy Bar Meter i jego konfiguracja,
- panelowy status Limit wraz z licznikami Low/High Failures,
- pełny stan Local/Remote,
- dostęp do plików pamięci urządzenia,
- pojemność pamięci przyjęta jako stałe 1000 albo 10000 wyników.

Dokumentacja nie daje jednoznacznego kontraktu dla tych funkcji. Aplikacja może później narysować własny bar, trend i wynik limitu na podstawie odebranych próbek, ale musi oznaczać je jako obliczenia lokalne.

## Architektura

Elementy wspólne:

- Shared/LabStation.Instruments/Transport/Vxi11Transport,
- Shared/LabStation.Instruments/Scpi/ScpiConnection,
- Shared/LabStation.Instruments/Discovery/InstrumentNetworkScanner,
- Shared/LabStation.Instruments/Scheduling/SerializedOperationGate,
- Shared/LabStation.UI/Controls/TimeSeriesPlot,
- wspólny motyw, przyciski, status, menu i okna Autor/Licencja.

Elementy własne SDM:

- MultimeterClient - identyfikacja modelu i komendy SDM,
- MultimeterSession - jeden właściciel transportu i cykl podglądu,
- MultimeterReadingParser - liczby, jednostki, overload i open,
- MultimeterConfigurationParser - funkcja, zakres i parametry,
- MultimeterStatisticsParser,
- MultimeterHistogramParser,
- model profilu możliwości SDM3055 i przyszłych wariantów.

Cykl podglądu powinien być operacją tła o niższym priorytecie. Jeżeli trwa ręczne pobranie pamięci przez "FETCh?", kolejne odświeżenie podglądu powinno zostać pominięte, a nie kolejkowane bez ograniczenia.

## Proponowane etapy

### 0.1.0 - bezpieczny podgląd

- połączenie, skan i Auto connect,
- "*IDN?",
- "DATA:LAST?",
- "DATA:POINts?",
- główny odczyt bez wykresu,
- automatyczne dopasowanie nazw, jednostek i statystyk do funkcji,
- lokalne statystyki serii,
- parsowanie przeciążenia,
- odczyt konfiguracji,
- test rzeczywistego urządzenia wyłącznie zapytaniami bez zmiany stanu.

### 0.2.0 - rozszerzony podgląd

- "FETCh?" dla zakończonej serii,
- statystyki miernika i lokalne,
- histogram,
- dodatkowe informacje o triggerze i pamięci,
- porównanie zachowania dla rzeczywistego firmware.

### Późniejszy tryb sterowania

- funkcja, zakres, NPLC, Null, trigger i logging,
- wyłącznie po osobnym zatwierdzeniu,
- wyraźnie oddzielony od panelu podglądu,
- z testami skutków każdej komendy na fizycznym mierniku.

## Test rzeczywistego SDM3055

Pierwszy test na 192.168.200.131 powinien być wyłącznie odczytowy:

1. "*IDN?" i sprawdzenie modelu.
2. "CONFigure?" i "[SENSe:]FUNCtion?".
3. "DATA:POINts?".
4. "DATA:LAST?".
5. Odczyt zapytań konfiguracji właściwych dla zwróconej funkcji.
6. Porównanie wyniku, jednostki i funkcji z panelem urządzenia.
7. Sprawdzenie sentinela dla overload/open bez celowego zmieniania obwodu.

Test nie używa "MEASure...?", "READ?", "INITiate", poleceń ustawiających ani czyszczących.

## Oficjalne źródła SIGLENT

- SDM Series Digital Multimeter Programming Guide EN02A: https://siglentna.com/wp-content/uploads/dlm_uploads/2017/10/SDM-Series-Digital-Multimeter_ProgrammingGuide_EN02A.pdf
- SDM3055 User Manual EN03B: https://siglentna.com/wp-content/uploads/dlm_uploads/2023/10/SDM3055_UserManual-EN03B.pdf
- Centrum dokumentów DMM: https://siglentna.com/resources/documents/digital-multimeter/
- Strona produktu SDM3055: https://www.siglent.com/in/products-overview/sdm3055/
