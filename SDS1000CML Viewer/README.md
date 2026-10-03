# Siglent SDS1000CML Viewer v0.7.3

Prosta aplikacja Windows do podglądu CH1/CH2 i pobierania przebiegów do CSV.
C# / WPF, szary interfejs zgodny z Koradem i czcionka Consolas. Bez EasyScopeX, NI-VISA, pakietów NI i zależności NuGet.

Główna powierzchnia przyrządu jest niezależnym panelem `OscilloscopeView`. Samodzielne okno EXE jest jego cienką powłoką, dzięki czemu ten sam panel może zostać osadzony bez uruchamiania drugiego programu w docelowym LabStation.

## Uruchamianie

Po zbudowaniu wydania rozpakuj archiwum i uruchom `Siglent.SDS1000CML.Viewer.exe`.
Wydanie zawiera środowisko .NET; SDK nie jest potrzebne do uruchomienia.
Aplikacja nie wymaga uprawnień administratora.

## Zakres

- Połączenie LAN przez VXI-11.
- Cykliczny podgląd przebiegów CH1 i CH2. Odznaczenie „Podgląd” zatrzymuje wyłącznie odświeżanie programu.
- Vpp, Vrms, częstotliwość, Vmin, Vmax i Duty są odczytywane dla wybranych i dostępnych kanałów.
- Cztery lokalne kursory pokazują czas i napięcie zaznaczonych kanałów oraz różnice dla kolejnych aktywnych par. Nieaktywny przycisk ma biały tekst, aktywny czarny, a podczas najechania tekst jest biały.
- Rolka myszy nad wykresem zmienia wyłącznie lokalny zakres osi czasu.
- „Pobierz przebieg” zachowuje pełny odebrany blok próbek w pamięci aplikacji.
- Menu `Zapisz jako` zapisuje ostatni ręcznie pobrany przebieg jako `CH1 CSV`, `CH2 CSV` albo `CH1 i CH2 CSV`. Pozycje wymagające nieobecnego kanału są nieaktywne. Późniejsze odświeżenia podglądu nie zastępują ręcznie pobranego przebiegu.
- Menu `Narzędzia` zawiera odczytowy skan sieci lokalnej oraz opcję `Auto connect`.
- Start, Stop i Auto Setup wysyłane tylko po kliknięciu.
- Program nie zmienia skali, wyzwalania, tłumienia sond ani stanu kanałów podczas łączenia i odczytu.
- Zaznaczenie CH1/CH2 wybiera kanały odczytywane. Kanał musi być włączony na oscyloskopie.

## Ethernet

1. Podłącz oscyloskop i komputer do tej samej sieci.
2. Ustaw adres IP oscyloskopu lub DHCP.
3. Wpisz IP i kliknij Offline albo użyj `Narzędzia > Skanuj sieć`.

Skan odpytuje urządzenia przez VXI-11 poleceniem `*IDN?`. Adres zostaje wpisany tylko wtedy, gdy odpowiedź identyfikuje obsługiwany model SDS1102CML+. Skan nie zmienia ustawień oscyloskopu. Opcja `Auto connect` łączy z niepustym zapisanym adresem po uruchomieniu programu oraz z adresem znalezionym przez skan. Przy pustym polu IP nie wykonuje połączenia.

Program używa VXI-11 (portmapper TCP 111 i port przydzielony przez urządzenie).
SDS1000CML+ nie obsługuje zwykłego SCPI socket na porcie 5025.
Domyślny adres urządzenia to `192.168.200.41`. Adres jest zapisywany lokalnie w `%LOCALAPPDATA%/Siglent SDS1000CML Viewer/settings.json`.

## Komunikacja

Program obsługuje wyłącznie połączenie LAN przez VXI-11. Kod transportu USB i testy ramek USBTMC zostały usunięte z projektu.
Aplikacja nie wymaga EasyScopeX, NI-VISA ani innych pakietów NI.

## Start / Stop / Auto

- Stop odczytuje dotychczasowy tryb wyzwalania, a następnie zatrzymuje akwizycję.
- Start wznawia odczytany lub zapamiętany tryb. Jeżeli urządzenie jest już zatrzymane
  i poprzedniego trybu nie udało się ustalić, Start wybiera `TRMD AUTO`.
- Auto oznacza `ASET` / Auto Setup i zmienia ustawienia pomiarowe urządzenia.
- Odczyt i zapis nie wykonują automatycznego Stop ani Auto.

Odczyty kanałów są sekwencyjne. Przy pracującym oscyloskopie CH1 i CH2 mogą
pochodzić z różnych akwizycji. Aby porównać zatrzymane przebiegi, naciśnij Stop
przed „Pobierz przebieg”. Program nie obiecuje ciągłego zapisu bez przerw.

## Wykres i kursory

- Rolka myszy nad wykresem przybliża lub oddala oś czasu względem położenia wskaźnika. Operacja jest lokalna i nie zmienia podstawy czasu oscyloskopu.
- Kliknięcie przycisku Kursor 1, Kursor 2, Kursor 3 albo Kursor 4 włącza i wybiera kursor. Ponowne kliknięcie aktualnie wybranego kursora wyłącza go.
- Prawy przycisk myszy nad wykresem odblokowuje wybrany kursor. Kursor podąża wtedy za wskaźnikiem.
- Lewy przycisk myszy ustawia odblokowany kursor w wybranym miejscu.
- Każdy aktywny kursor pokazuje czas oraz napięcie zaznaczonych kanałów. Odznaczony kanał natychmiast znika z wykresu, pomiarów i odczytów kursorów.
- Aktywne kursory są parowane według numerów: 1-2, następnie 3-4. Jeżeli aktywne są tylko 2 i 3, tworzą parę 2-3. Kursor bez pary nie ma delty.
- Dla pary pokazywane są delta czasu oraz delty napięcia zaznaczonych kanałów.
- Po odznaczeniu obu kanałów wykres i wiersze pomiarowe są puste, a przyciski kursorów są nieaktywne.

## CSV i skalowanie

Kolumny: `channel,time_s,voltage_V,captured_utc`.
Czas w sekundach pochodzi z deskryptora przebiegu, napięcie w woltach.
`captured_utc` to czas odbioru danych przez komputer, nie sprzętowy znacznik wyzwolenia.
Przecinek oddziela kolumny, kropka jest separatorem dziesiętnym. Import do polskiego
Excela należy wykonać przez import CSV z tymi ustawieniami.

Program pobiera `C1:WF? ALL` / `C2:WF? ALL`, odczytuje blok `WAVEDESC`,
kolejność bajtów, szerokość próbek, gain, offset i interwał czasu.
Nie używa wzoru czasu z nowszych modeli X-E.
Ustawia wyłącznie parametry transferu `WFSU SP,1,NP,0,FP,0`.
Nie zakłada, że każda odpowiedź ma maksymalną głębokość pamięci urządzenia:
liczba rzeczywiście odebranych punktów jest widoczna w programie.

Wykres stosuje min/max przy ograniczaniu punktów do szerokości ekranu.
CSV zawiera wszystkie odebrane próbki, bez tego ograniczenia.
Niepełny lub nierozpoznany deskryptor jest odrzucany.

## Stan wersji 0.7.3

W wersji 0.7.3 ikona aplikacji zawiera osobne, zachowujące proporcje i przezroczystość klatki 16, 32 i 256 px.

## Stan wersji 0.7.2

W wersji 0.7.2 ujednolicono stany tekstu przycisków kursorów ze wspólnym standardem LabStation. Status ma biały tekst dla stanów normalnych, a czerwony wyłącznie dla błędu.

## Stan wersji 0.7.1

W wersji 0.7.1 przyciski ogólne mają wspólny, kompaktowy rozmiar Korada i rozszerzają się tylko wtedy, gdy wymaga tego treść. Aktywny przycisk kursora nie otrzymuje dodatkowej obwódki. Checkboxy CH1 i CH2 zostały połączone bezpośrednio z odpowiadającymi im wierszami pomiarów, a wyłączony kanał pozostawia zwarty wiersz z samą nazwą. Dostępność eksportu CSV zależy od wyboru kanałów, nie od obecności danych, dlatego dopuszczalny jest również plik zawierający sam nagłówek. Menu `Zapisz jako` nie reaguje już na okresową zmianę stanu podglądu, co usuwa jego migotanie. Etykieta stanu została skrócona do `Status:`.

## Stan wersji 0.7.0

W wersji 0.7.0 przyciski ogólne i przyciski czterech kursorów zostały ujednolicone ze standardem Korada. Menu `Narzędzia` udostępnia skan lokalnej sieci i zapamiętywaną opcję `Auto connect`. Menu `Zapisz jako` zastępuje dolny przycisk CSV i niezależnie udostępnia eksport CH1, CH2 albo obu kanałów zgodnie z zawartością ostatniego ręcznego pobrania. Separator menu korzysta ze wspólnego, dopasowanego kolorystycznie stylu `LabStation.UI`.

Skanowanie korzysta ze wspólnej biblioteki `Shared/LabStation.Instruments`, ogranicza duże podsieci do lokalnego segmentu `/24`, izoluje błędy poszczególnych adresów i przyjmuje wyłącznie model zatwierdzony przez `Scope.Core`. Rzeczywisty test odczytowy wykrył SDS1102CML+ pod `192.168.200.41` za pomocą samego `*IDN?`. Test skanowania nie zmieniał ustawień ani stanu akwizycji urządzenia.

## Stan wersji 0.6.2

W wersji 0.6.2 transport VXI-11, kontrakt SCPI i serializacja operacji pochodzą ze wspólnej biblioteki `Shared/LabStation.Instruments`. Wykres korzysta ze wspólnej kontrolki `TimeSeriesPlot`, zachowując cztery kursory, lokalne powiększenie, kanały CH1 i CH2 oraz pełną specyfikę dekodowania `WAVEDESC` w Oscyloskopie.

## Stan wersji 0.6.1

W wersji 0.6.1 adres IP, przycisk Offline/Online, wybór kanałów, Podgląd i cztery kursory znajdują się w jednym wierszu. Przyciski kursorów zajmują cztery równe, elastyczne kolumny i pozostają czytelne przy minimalnym rozmiarze okna. Test UI sprawdza rzeczywiste położenie kontrolek oraz brak przycinania przycisków w obu obsługiwanych rozmiarach.

## Stan wersji 0.6.0

W wersji 0.6.0 motyw, kontrolki bazowe, menu `O aplikacji`, okna Autor/Licencja oraz obsługa systemowego motywu zostały przeniesione do wspólnej biblioteki `Shared/LabStation.UI`. Rozwijane menu używa własnego szablonu bez zewnętrznej ramki. Logika sesji, transport VXI-11, obsługa dużych bloków przebiegu, pomiary, wykres i kursory pozostają częścią Oscyloskopu.

W wersji 0.5.0 interfejs został przeniesiony z WinForms do WPF. Usunięto listę metody połączenia, ponieważ aplikacja obsługuje wyłącznie LAN. Powłoka, paleta, kontrolki, menu `O aplikacji` oraz osobne okna Autor i Licencja zostały ujednolicone z Koradem. Wykres, pomiary, lokalne powiększenie i mechanika czterech kursorów pozostały funkcjonalnością właściwą oscyloskopowi.

Zmiany UI zostały zweryfikowane testami programowymi i zrzutami w dwóch rozmiarach okna. Dla wersji 0.7.0 wykonano wyłącznie rzeczywisty test skanowania `*IDN?`. Poniższe testy przebiegów i poleceń dotyczą wcześniejszej wersji 0.4.1 korzystającej z tego samego `Scope.Core` i transportu VXI-11.

Połączenie LAN zostało sprawdzone na fizycznym SIGLENT SDS1102CML+ z firmware
6.01.01.25. W bieżącej wersji test odczytowy pobrał po 20 480 punktów z CH1 i CH2,
stan `SAST?` oraz Vpp, Vrms, częstotliwość, Vmin, Vmax i Duty dla obu kanałów.
Pełny cykl trwał 337 ms. Stan urządzenia przed testem i po nim wynosił START.
Wcześniejszy test objął również Start, Stop oraz utworzenie 40 961 wierszy CSV.
Auto Setup nie był wykonywany w teście sprzętowym, ponieważ zmienia konfigurację
pomiaru oscyloskopu.

Podczas testu aktywnego podglądu pojedyncze polecenie Stop zostało zachowane
w kolejce i wykonane po bieżącym transferze w 396 ms. Oczekujące polecenie
zablokowało rozpoczęcie następnego odświeżenia. Po teście przywrócono początkowy
stan Stop.

Testy programowe obejmują bloki binarne, podpisane próbki, skalowanie deskryptora,
CSV, odczyt bez zmiany ustawień, odpowiedzi `SAST`,
kolejność podgląd-polecenie, parser parametrów PAVA, predykat obsługi modelu oraz sesję VXI-11 przez lokalny TCP. Test UI uruchamia rzeczywistą powierzchnię WPF i weryfikuje panel wielokrotnego użytku, brak listy LAN, wspólny styl Korada, ikonę i nazwę aplikacji, układ w dwóch rozmiarach okna, dostępność rozłączenia podczas aktywnego podglądu, lokalne powiększanie osi czasu, parowanie kursorów, widoczność danych po wyłączeniu CH1 i CH2, selektywną dostępność CSV, skanowanie i Auto connect na atrapach oraz menu i okna `O aplikacji`.

## Budowanie

Windows i SDK .NET 10:

```powershell
./build.ps1
./build.ps1 -Publish
dotnet run --project tests/Scope.UiTests -c Release
```

Kod: tabulatory, klamry w nowej linii, bez przeformatowywania niezwiązanych fragmentów.
`./build.ps1 -Publish` tworzy samodzielny plik EXE w `artifacts/final/win-x64` oraz archiwum ZIP w `artifacts/final`.

## Licencja

[PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0).
Pełny, niezmieniony tekst w pliku LICENSE. Kod aplikacji jest dostępny do zastosowań
dozwolonych tą licencją. Środowisko .NET zachowuje własne licencje i informacje.
Consolas jest używana z systemu Windows; plik czcionki nie jest dystrybuowany.

## Źródła techniczne

- [SIGLENT CML+/DL+ User Manual](https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2020/12/SDS1000CML_UserManual_UM0101A-E02B.pdf)
- [SIGLENT Remote Programming Manual dla CML/CML+](https://siglentna.com/wp-content/uploads/dlm_uploads/2017/10/ProgrammingGuide_forSDS-1-1.pdf)
- [SIGLENT Programming Guide EN02E](https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2025/11/SDS1000-SeriesSDS2000XSDS2000X-E_ProgrammingGuide_EN02E.pdf)
- [SIGLENT LAN/VXI-11](https://siglentna.com/operating-tip/lan-setup-on-a-sds1000-series/)
- [SIGLENT - obsługa portów](https://siglentna.com/operating-tip/instrument-socket-and-telnet-port-information/)
- [VXI-11 definicje protokołu](https://github.com/python-ivi/python-vxi11/blob/master/vxi11/vxi11.py)
- [sigrok - porównanie formatu starszych SDS](https://github.com/sigrokproject/libsigrok/tree/master/src/hardware/siglent-sds)

Implementacja własna; nie skopiowano kodu sigrok ani python-vxi11.
