# Zakres panelu Siglent SDM3000 Control

Stan dokumentu: zaimplementowane i sprawdzone programowo oraz na fizycznym SDM3055 wydanie 0.2.0.

## Cel

Aplikacja utrzymuje jedną sesję VXI-11, ciągle inicjuje świeże pomiary i pokazuje bieżącą wartość. Osiem przycisków pozwala wybrać napięcie AC/DC, prąd AC/DC, rezystancję, pojemność, diodę lub ciągłość. Aplikacja nie zmienia zakresu, NPLC, filtrów, limitów ani konfiguracji sieci.

Test sprzętowy wykazał, że po nawiązaniu sesji VXI-11 miernik przechodzi w Remote i zmienia Auto trig na Stopped. W tym stanie pasywne `DATA:LAST?` nie zapewnia świeżych wyników. Dlatego aplikacja świadomie używa `READ?` i jest kontrolerem ciągłych pomiarów, a nie pasywnym viewerem.

## Cykl pomiarowy

Po połączeniu aplikacja wykonuje:

1. jedno połączenie VXI-11 utrzymywane przez całą sesję,
2. `CONFigure?` w celu rozpoznania funkcji i zakresu,
3. `READ?` w celu rozpoczęcia i odebrania świeżego pomiaru,
4. `DATA:POINts?` w celu odczytania liczby punktów pamięci,
5. powtórzenie odczytu po około 100 ms,
6. `device_local` i zamknięcie transportu dopiero przy rozłączeniu.

`READ?` rozpoczyna sekwencję pomiarową i czyści sprzętową pamięć odczytów. Jest to zaakceptowany skutek działania aplikacji. Lokalna historia, statystyki i eksport CSV pozostają niezależne od pamięci urządzenia.

## Potwierdzony test sprzętowy

Urządzenie pod adresem `192.168.200.131` zwróciło:

- producent: Siglent Technologies,
- model: SDM3055,
- firmware: `1.02.01.29R1`.

Trzy kolejne pomiary wykonane ścieżką produkcyjną `READ?` w jednej trwałej sesji zwróciły:

- 11,30298 mV,
- 11,30100 mV,
- 11,30511 mV.

W osobnym kontrolowanym teście zmiana napięcia zasilacza Korad z 10 V na 11 V dała odpowiednio około 9,992 V i 10,991 V z SDM. Wcześniejszy test `DATA:LAST?` pozostawał na tej samej starej wartości, co potwierdziło konieczność jawnego inicjowania pomiaru.

## Firmware

Na dzień 2026-10-03 najnowszym firmware opublikowanym przez SIGLENT dla SDM3055 jest `V1.02.01.29R1`. Sprawdzony miernik ma tę samą wersję.

Oficjalna lista zmian tego wydania obejmuje:

- automatyczne włączanie po podaniu zasilania,
- poprawkę nieprawidłowej odpowiedzi `ROUT:DATA?`,
- zapis dolnego limitu skanera po restarcie,
- poprawkę możliwej utraty danych i zawieszenia podczas logowania na USB.

Lista zmian nie wymienia Remote, Stopped, Auto Trigger, `device_local`, VXI-11 ani zachowania `READ?`. Aktualizacja do najnowszej wersji nie usunęła obserwowanego problemu.

## Zachowanie Local i Remote

Transport wykonuje procedurę `device_local` przy końcowym rozłączeniu. Na tym egzemplarzu i firmware nie powoduje to widocznego wyjścia z Remote. Fizyczne naciśnięcie Shift wyłącza Remote tylko do następnego polecenia wysłanego przez aplikację.

Z tego powodu aplikacja:

- nie twierdzi, że programowo przywraca panel lokalny,
- utrzymuje jedno połączenie przez cały czas sesji,
- pokazuje świeże pomiary przez `READ?`,
- zmienia funkcję tylko po użyciu jednego z ośmiu przycisków, ale nie zmienia zakresu.

## Interfejs

- poziome, nierozciągalne okno 920 x 318 px,
- pole IP 150 px i standardowy przycisk Offline/Online,
- menu Narzędzia z pozycjami Skanuj sieć i Auto connect,
- automatyczna identyfikacja funkcji oraz osiem przycisków jej wyboru,
- duży wynik z właściwą nazwą i jednostką,
- lokalne minimum, maksimum, średnia, peak-to-peak i odchylenie standardowe,
- menu Zapisz jako z eksportem CSV,
- Status z białym tekstem, czerwonym tylko przy błędzie,
- przyciski `V AC`, `V DC`, `A AC`, `A DC`, `Ω`, `F`, `Diod` i `Sig`.

## Obsługiwane funkcje

- napięcie DC i AC True RMS,
- prąd DC i AC True RMS,
- rezystancja dwu- i czteroprzewodowa,
- pojemność,
- częstotliwość i okres,
- temperatura,
- ciągłość,
- test diody.

## Architektura

Elementy wspólne:

- `Shared/LabStation.Instruments/Transport/Vxi11Transport`,
- `Shared/LabStation.Instruments/Scpi/ScpiConnection`,
- `Shared/LabStation.Instruments/Discovery/InstrumentNetworkScanner`,
- wspólny motyw, menu, przyciski i okna Autor/Licencja.

Elementy własne SDM:

- `SiglentMultimeterClient` - identyfikacja modelu i polecenia SDM,
- `MultimeterSession` - jedna serializowana sesja, konfiguracja funkcji i lokalny akumulator pomiarów,
- parser konfiguracji, wyników i przeciążenia,
- profile nazw oraz jednostek pomiarowych,
- eksport lokalnej historii do CSV.

## Oficjalne źródła SIGLENT

- SDM Series Digital Multimeter Programming Guide EN02A: https://siglentna.com/wp-content/uploads/dlm_uploads/2017/10/SDM-Series-Digital-Multimeter_ProgrammingGuide_EN02A.pdf
- SDM3055 User Manual EN03B: https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2023/10/SDM3055_UserManual-EN03B.pdf
- Firmware i oprogramowanie multimetrów: https://www.siglenteu.com/service-and-support/firmware-software/digital-multimeters/
- Strona produktu SDM3055: https://www.siglent.com/products-overview/sdm3055/
