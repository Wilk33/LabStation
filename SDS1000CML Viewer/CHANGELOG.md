# Zmiany

## 0.7.3 - 2026-10-03

- Zastąpiono jednoklatkową ikonę wielorozmiarowym ICO z osobnymi klatkami 16, 32 i 256 px.

## 0.7.2 - 2026-10-01

- Nieaktywny przycisk kursora ma biały tekst, aktywny czarny, a podczas najechania tekst jest biały.
- Status ma biały tekst we wszystkich stanach normalnych i czerwony tekst przy błędzie.

## 0.7.1 - 2026-09-30

- Ustanowiono kompaktowy rozmiar przycisków Korada jako wspólny standard; większa szerokość wynika wyłącznie z zawartości.
- Usunięto dodatkową obwódkę aktywnego przycisku kursora.
- Przeniesiono checkboxy CH1 i CH2 bezpośrednio do wierszy pomiarów oraz zachowano nazwę kanału po jego wyłączeniu.
- Powiązano dostępność pozycji CSV ze stanem checkboxów kanałów, niezależnie od zawartości ostatniego przebiegu.
- Dopuszczono eksport pustego kanału do poprawnego pliku CSV zawierającego nagłówek.
- Odseparowano stan menu `Zapisz jako` od cyklu podglądu, usuwając jego migotanie.
- Skrócono `Stan oscyloskopu:` do `Status:`.

## 0.7.0 - 2026-09-30

- Ujednolicono wymiary głównych przycisków i czterech przycisków kursorów ze standardem Korada.
- Dodano menu `Narzędzia` z odczytowym skanem sieci lokalnej i zapamiętywaną opcją `Auto connect`.
- Przeniesiono ogólny mechanizm wykrywania urządzeń VXI-11 do `Shared/LabStation.Instruments`; obsługiwany model nadal wybiera `Scope.Core`.
- Zastąpiono dolny przycisk CSV menu `Zapisz jako` z osobnymi pozycjami dla CH1, CH2 oraz obu kanałów.
- Pozycje eksportu są aktywne wyłącznie wtedy, gdy ostatni ręcznie pobrany przebieg zawiera wymagane kanały.
- Ujednolicono wygląd separatorów menu w `Shared/LabStation.UI`.
- Test odczytowy rzeczywistej sieci wykrył obsługiwany SDS1102CML+ pod `192.168.200.41` poleceniem `*IDN?`.

## 0.6.2 - 2026-09-29

- Przeniesiono transport VXI-11 i podstawowy kontrakt SCPI do `Shared/LabStation.Instruments`.
- Zastąpiono lokalny mechanizm rysowania wspólną kontrolką `TimeSeriesPlot`, zachowując cztery kursory, powiększanie oraz kanały CH1 i CH2.
- Parser `WAVEDESC`, pomiary i polityka podglądu pozostały lokalnymi elementami Oscyloskopu.

## 0.6.1 - 2026-09-29

- Połączono adres IP, przycisk Offline/Online, wybór CH1 i CH2, Podgląd oraz cztery kursory w jednym wierszu.
- Kursory korzystają z czterech równych, elastycznych kolumn, dzięki czemu napisy pozostają czytelne także przy minimalnej szerokości okna.
- Test UI mierzy rzeczywiste położenie kontrolek i odrzuca ponowne rozdzielenie ich na dwa wiersze.

## 0.6.0 - 2026-09-29

- Przeniesiono motyw, kontrolki bazowe, obsługę systemowego motywu oraz okna Autor i Licencja do `Shared/LabStation.UI`.
- Zastąpiono lokalną definicję menu wspólnym komponentem `AboutMenuItem`.
- Usunięto zewnętrzną ramkę widoczną po rozwinięciu menu.
- Zachowano niezależny panel `OscilloscopeView` oraz całą specyficzną obsługę VXI-11, dużych bloków danych, wykresu i kursorów.
- Test UI sprawdza rzeczywiste użycie biblioteki wspólnej, komponentu menu i brak ramki rozwiniętego menu.

## 0.5.0 - 2026-09-29

- Przeniesiono całą warstwę interfejsu z WinForms do WPF, zgodnie z architekturą Korada.
- Główna powierzchnia oscyloskopu jest niezależnym panelem `OscilloscopeView`, a samodzielne okno EXE pełni rolę cienkiej powłoki.
- Zastosowano paletę, krój pisma, styl przycisków, pól tekstowych i menu zgodne z Koradem.
- Ujednolicono menu `O aplikacji` oraz osobne okna Autor i Licencja, łącznie z nazwą i wersją aplikacji w paskach tytułu.
- Usunięto listę wyboru `LAN`. Adres IP i przycisk Offline/Online są jedynymi elementami połączenia.
- Przeniesiono wykres, lokalne powiększenie i pełną mechanikę czterech kursorów do natywnej kontrolki WPF.
- Zachowano transport VXI-11, kolejkę operacji, pomiary, podgląd, ręczne pobieranie i zapis CSV.
- Dodano test uruchomieniowy rzeczywistego panelu WPF w dwóch rozmiarach okna.
- Nie dodano nowych stale widocznych opisów ani instrukcji w interfejsie.

## Integracja z LabStation - 2026-09-29

- Przeniesiono aplikację do katalogu `SDS1000CML Viewer` jako niezależny program, rozwijany obecnie jako `Siglent SDS1000CML Viewer v0.6.1`.
- Usunięto transport USB i testy USBTMC. Jedyną metodą komunikacji jest LAN przez VXI-11.
- Żądanie Offline jest dostępne podczas aktywnego podglądu i czeka w kolejce za trwającym odczytem zamiast być pomijane.
- Ustawiono domyślny adres oscyloskopu `192.168.200.41` oraz osobny katalog ustawień aplikacji.
- Nie dodano żadnych nowych stale widocznych opisów ani instrukcji w interfejsie.

## 0.4.1 - 2026-09-28

- Usunięto wyłącznie stale widoczną instrukcję `PPM odblokuj -> LPM ustaw` przy przyciskach kursorów. Podpowiedzi wyświetlane po najechaniu myszką pozostają dostępne.
- Usunięto cały dodatkowy dolny wiersz komunikatów, w tym komunikaty o rozłączeniu, oczekiwaniu, pobraniu i zapisaniu przebiegu.
- Zachowano żądany wiersz stanu oscyloskopu `START`, `STOP`, `NIEZNANY` lub `OFFLINE` oraz wiersz informacji o pobranych punktach.
- Odznaczony kanał natychmiast znika z wykresu, wierszy pomiarowych i odczytów kursorów.
- Po odznaczeniu obu kanałów wykres nie pokazuje przebiegów ani pomiarów, a przyciski czterech kursorów są nieaktywne.
- Dodano test regresyjny widoczności CH1 i CH2 oraz braku usuniętych elementów interfejsu.
## 0.4.0 - 2026-09-28

- Dodano cztery kursory pomiarowe. Wybrany kursor odblokowuje się prawym przyciskiem myszy, podąża za wskaźnikiem i jest ustawiany lewym przyciskiem.
- Każdy kursor pokazuje czas oraz napięcie CH1 i CH2. Aktywne kursory są łączone kolejno w pary, dla których obliczane są różnice czasu i napięcia obu kanałów.
- Rolka myszy nad wykresem przybliża lub oddala wyłącznie lokalną oś czasu aplikacji i nie wysyła poleceń do oscyloskopu.
- Dodano odczyt Vpp, Vrms, częstotliwości, Vmin, Vmax i współczynnika wypełnienia dla każdego pobranego kanału.
- Ręczne pobranie przebiegu jest kolejkowane za trwającym podglądem, dzięki czemu pierwsze kliknięcie nie jest pomijane.
- Usunięto dolny komunikat o aktywnym podglądzie.
- Zastosowano obsługę ciemnego paska tytułu z projektu KA3005P App na etapie tworzenia uchwytu okna. Wymuszane jest także natychmiastowe przerysowanie ramki głównego okna oraz okien Autor i Licencja.
- Odczyt przebiegów, parametrów i stanu zweryfikowano na fizycznym SDS1102CML+ z firmware 6.01.01.25. Cykl obejmujący oba kanały trwał 337 ms i nie zmienił stanu START urządzenia.

## 0.3.0 - 2026-09-28

- Dodano stale widoczny stan akwizycji `START`, `STOP`, `NIEZNANY` lub `OFFLINE`, odczytywany poleceniem `SAST?`.
- Polecenia Start, Stop i Auto są kolejkowane za trwającym odczytem, dlatego pierwsze kliknięcie nie ginie podczas aktywnego podglądu.
- Oczekujące polecenie blokuje rozpoczęcie kolejnego automatycznego odświeżenia.
- Pasek tytułu, normalne menu i standardowe kontrolki dziedziczą motyw Windows. Dodano obsługę ciemnego paska DWM i menu na Windows 10.
- Dodano test kolejności operacji, test odpowiedzi `SAST` i kontrolę ciemnego paska tytułu.
- Połączenie LAN, odczyt CH1/CH2, CSV oraz Start/Stop zweryfikowano na fizycznym SDS1102CML+ z firmware 6.01.01.25.

## 0.2.1 - 2026-09-28

- Przycisk połączenia pokazuje wyłącznie status Offline albo Online.
- Usunięto komunikat o USB z głównego okna.
- Zastąpiono niestandardowy wygląd menu standardowym paskiem systemowym Windows.
- Usunięto grafiki, ikony paska tytułu i przyciski Zamknij z okien Autor i Licencja.
- Okna informacyjne zamyka się standardowym przyciskiem X na pasku tytułu.

## 0.2.0 - 2026-09-28

- Dodano przekazane ikony PNG i ICO do aplikacji oraz okien informacyjnych.
- Przeniesiono status Offline/Online do przycisku Połącz/Rozłącz.
- Usunięto osobny napis Offline i opis podglądu.
- Dodano pasek `O Aplikacji` z pozycjami Autor i Licencja.
- Dodano okno autora: Mateusz Skipor, Inżynier Technik Elektroniki,
  mskiporsklep@op.pl.
- Dodano okno z pełnym tekstem PolyForm Noncommercial License 1.0.0.
- Zablokowano wybór USB i oznaczono tę metodę jako nietestowaną oraz niewdrożoną.
- Rozszerzono test UI o nowe zachowania i dwa rozmiary okna.

## 0.1.0 - 2026-09-27

Pierwsze wydanie aplikacji Windows:

- Szare okno WinForms, Consolas i wykres CH1/CH2.
- Połączenie VXI-11 przez Ethernet i USBTMC przez WinUSB bez NI.
- Pobieranie bloków WAVEDESC i eksport CSV.
- Jawne Start/Stop i Auto Setup.
- Oddzielenie zapisanego przebiegu od aktualizowanego podglądu.
- Testy dekodowania, transportu, poleceń i interfejsu.

Wersja zweryfikowana programowo, bez testu na fizycznym oscyloskopie.
