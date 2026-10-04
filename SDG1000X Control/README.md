# Siglent SDG1000X Control v0.2.10

Wąska aplikacja Windows do podstawowego sterowania dwukanałowym generatorem SIGLENT SDG1032X przez LAN/VXI-11. Główna powierzchnia `GeneratorView` jest panelem wielokrotnego użytku, a samodzielne EXE jest jego cienką powłoką.

## Najważniejsze założenia

- Okno ma stały rozmiar 350 x 745 px, pozwala się minimalizować i nie pozwala zmieniać rozmiaru ani maksymalizować. W trybach prostokąta i rampy status znajduje się bezpośrednio pod selektorem mnożnika, bez pionowego paska przewijania.
- CH1 i CH2 są dostępne na dwóch zakładkach zajmujących po połowie szerokości, więc widoczny jest jeden kanał jednocześnie.
- Aktywna zakładka ma kolor kanału i czarny tekst, a nieaktywna biały tekst.
- Przyciski połączenia i wyjścia nie zmieniają koloru wraz ze stanem. Stan jest podawany wyłącznie tekstem `Online`/`Offline` oraz `ON`/`OFF`.
- Nagłówki zakładek pokazują stan wyjścia, częstotliwość i Vpp w skróconej notacji inżynierskiej.
- Pola parametrów są ułożone pionowo.
- Każda wartość ma przyciski góra/dół z automatycznym powtarzaniem.
- Enter zatwierdza wartość wpisaną z klawiatury.
- Przyciski góra/dół wysyłają zmianę bez dodatkowego zatwierdzania.
- Nie ma przycisku Zastosuj.
- Operacje sieciowe nie blokują wątku interfejsu.
- Szybkie zmiany tego samego parametru są łączone, a do urządzenia trafia najnowsza oczekująca wartość.
- Strzałki domyślnie zmieniają wartość o najmniejszy widoczny krok. Selektor `G`, `M`, `k`, `1`, `m`, `u`, `n` na dole kanału wybiera większy lub mniejszy krok we wskazanej jednostce.
- Aktywny mnożnik ma zielone tło i czarny tekst. Nieaktywne mnożniki zachowują standardowy wygląd przycisku i biały tekst. Podczas najechania tekst każdego mnożnika jest biały.
- Zwykły status ma biały tekst, a czerwony kolor jest zarezerwowany dla błędów.
- Samodzielny plik wykonywalny ma ogólną nazwę `Siglent.SDG1000X.Control.exe`, bez oznaczenia konkretnego modelu.
- Mnożnik nie pozwala zejść poniżej najmniejszego widocznego kroku pola, a każda zmiana pozostaje ograniczona do zakresu nastawy.

## Obsługiwane ustawienia

Przebiegi:

- sinus,
- prostokąt,
- rampa,
- impuls,
- szum,
- DC,
- `Arbitralne` z pliku EasyWave CSV albo binarnego pliku SIGLENT.

`Arbitralne` pozostaje pozycją listy `Przebieg`. Po wybraniu tej pozycji aplikacja natychmiast wysyła `BSWV WVTP,ARB`, dzięki czemu cykliczny odczyt nie przywraca poprzedniego typu przebiegu przed wgraniem pliku. Bezpośrednio pod listą pojawiają się pole ścieżki, przycisk z ikoną katalogu i przycisk `Wczytaj`.

Obsługiwane są dwa formaty:

- `.csv` eksportowany przez EasyWave - aplikacja sprawdza metadane, kolumny `xpos,value`, deklarowaną liczbę od 2 do 16384 próbek oraz przelicza wartości napięcia na 14-bitowe próbki SDG1000X,
- `.bin` - kolejne 14-bitowe wartości ze znakiem zapisane jako 16-bitowe słowa little-endian.

Po wczytaniu aplikacja wysyła dane poleceniem `WVDT` i wybiera zapisany przebieg poleceniem `ARWV`. Częstotliwość, amplituda, offset i faza wysyłane z przebiegiem pozostają wartościami ustawionymi w panelu kanału. Metadane amplitudy i offsetu z EasyWave służą do prawidłowego przeliczenia kolumny `value` na kod 14-bitowy.

Parametry:

- częstotliwość,
- amplituda,
- offset,
- faza z krokiem 0,0001°,
- wypełnienie prostokąta z krokiem 0,001%,
- symetria rampy z krokiem 0,1%,
- szerokość impulsu,
- zbocze narastające impulsu z krokiem 0,1 ns,
- opóźnienie impulsu z krokiem 0,000001 s,
- odchylenie standardowe szumu z krokiem 0,0001 V i średnia szumu,
- poziom DC,
- obciążenie Hi-Z lub 50 Ω,
- polaryzacja normalna lub odwrócona,
- niezależny stan wyjść CH1 i CH2.

Modulacje, sweep, burst i pozostałe funkcje zaawansowane celowo nie są obsługiwane.

## Połączenie

Aplikacja używa VXI-11 przez LAN, tej samej metody połączenia co referencyjna aplikacja oscyloskopu.

1. Podaj adres IP generatora albo wybierz `Narzędzia -> Skanuj sieć`.
2. Kliknij przycisk Offline.
3. Po połączeniu aplikacja sprawdzi odpowiedź *IDN?.
4. Akceptowany jest model SIGLENT SDG1032X.
5. Ustawienia obu kanałów zostaną odczytane automatycznie.

Podczas aktywnego połączenia pełne ustawienia CH1 i CH2 są ponownie odczytywane co 1 sekundę. Zmiana wykonana z panelu generatora pojawia się dzięki temu w aplikacji bez ponownego łączenia. Ochrona wpisywanego tekstu działa tylko podczas edycji pola i nie blokuje późniejszych odświeżeń całego kanału. Pojedynczy błąd odczytu nie zatrzymuje kolejnych cykli.

Opcja `Narzędzia -> Auto connect` zapisuje się razem z adresem. Przy uruchomieniu łączy aplikację z niepustym zapisanym adresem. Po skanowaniu automatycznie łączy z odnalezionym generatorem, jeśli opcja jest zaznaczona.

Transport VXI-11, podstawowe operacje SCPI oraz kolejka `latest-wins` pochodzą ze wspólnej biblioteki `Shared/LabStation.Instruments`. Polecenia kanałów, parsery generatora i reguły wyjść pozostają lokalne.

Aplikacja nie zawiera trybu demonstracyjnego ani symulowanego urządzenia.

## Budowanie i testy

Wymagany jest .NET 10 SDK dla Windows.

    dotnet run --project tests/Sdg1032X.Tests/Sdg1032X.Tests.csproj -c Release
    dotnet build SDG1000X.Control.slnx -c Release
    dotnet publish src/Sdg1032X.App/Sdg1032X.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

## Status weryfikacji

Podstawowy zestaw testów automatycznych nie łączy się z fizycznym urządzeniem. Zakres walidacji sprzętowej oraz pełna procedura testowa są opisane w [docs/hardware-testing.md](docs/hardware-testing.md). Wersja 0.2.10 przeszła 29/29 testów, włącznie z parserem rzeczywistego układu pliku EasyWave CSV, kodowaniem próbek, precyzją nowych nastaw impulsu i szumu, pełnym układem panelu Impuls, poprawnym wyglądem mnożnika Offline, minimalizacją bez zmiany rozmiaru, przełączeniem na ARB przed wgraniem pliku oraz odpornym na przejściowy błąd cyklem odczytu obu kanałów co 1 sekundę.

Na fizycznym SDG1032X test wybrał CH2 z przebiegiem sinusoidalnym i wyłączonym wyjściem. Kontrolka UI rozpoczęła od 1000 Hz, przyjęła zewnętrzne zmiany do 1111 Hz i 1222 Hz po kolejnych cyklach, a następnie test przywrócił 1000 Hz i potwierdził `OUTPUT=OFF`. Wysyłanie pliku przebiegu arbitralnego nie zostało wykonane na fizycznym generatorze w ramach tego wydania.

## Autor i licencja

- Mateusz Skipor
- Inżynier technik elektroniki
- mskiporsklep@op.pl

Projekt jest udostępniany na warunkach [PolyForm Noncommercial License 1.0.0](LICENSE).

## Dokumentacja protokołu

- [SIGLENT SDG Series Programming Guide](https://siglentna.com/wp-content/uploads/dlm_uploads/2017/10/SDG_Programming_Guide.pdf)
- [SIGLENT - szablon i opis EasyWave CSV](https://www.siglenteu.com/operating-tip/custom-waveforms-using-easywave-csv-templates/)
- [SIGLENT - binarne próbki little-endian 2's complement dla SDG1000X](https://siglentna.com/application-note/programming-example-create-a-stair-step-waveform-using-matlab-sdg1000x-sdg2000x-sdg6000x/)
- [SIGLENT - przykład przesyłania własnego przebiegu przez LAN](https://www.siglenteu.com/application-note/programming-example-create-a-stair-step-waveform-using-python-and-pyvisa-using-lan/)
- [SIGLENT SDG1000X](https://www.siglent.com/eu/products-overview/sdg1000x/)
