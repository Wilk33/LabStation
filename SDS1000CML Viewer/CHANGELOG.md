# Zmiany

## Integracja z LabStation - 2026-09-29

- Przeniesiono aplikację do katalogu `SDS1000CML Viewer` jako niezależny program `Siglent SDS1000CML Viewer v0.4.1`.
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
