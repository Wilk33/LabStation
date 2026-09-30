# Korad KA3005P

Samodzielna aplikacja Windows do sterowania jednym lub dwoma zasilaczami laboratoryjnymi Korad KA3005P przez porty COM.

Bieżąca wersja: `0.2.3`. Powierzchnie pojedynczego i podwójnego zasilacza pozostają niezależnymi panelami WPF. Wspólny motyw, kontrolki nastaw, listy, tekstowe paski stanu, menu, wykres oraz okna Autor/Licencja pochodzą z `Shared/LabStation.UI`. Komunikacja COM, kolejki sesji i logika trybu Dual pozostają częścią Korada.

## Zakres

- Tryb pojedynczego zasilacza.
- Tryb Dual: szeregowy, równoległy i symetryczny.
- Automatycznie odświeżana lista portów COM.
- Blokada użycia tego samego portu przez dwie sesje.
- Nastawy napięcia i ograniczenia prądu.
- Sterowanie wyjściem ON i OFF.
- Odczyt napięcia i prądu wyjściowego.
- Wykres prądu i opcjonalny wykres napięcia, lokalne powiększanie oraz dwa kursory dostępne po wyłączeniu wyjścia.
- Do 20 480 widocznych próbek, zgodnie z limitem Oscyloskopu.
- Oś czasu wykresu zlicza wyłącznie czas aktywnego pomiaru w stanie ON. Przejście do OFF zachowuje historię i nie dopisuje przerwy do osi czasu.
- Przy nominalnym okresie próbkowania 100 ms limit 20 480 próbek odpowiada 34 min 8 s aktywnego pomiaru. Rzeczywisty zakres może być dłuższy, jeśli odpowiedzi urządzenia opóźniają kolejne próbki.
- W trybie symetrycznym niezależne dane i serie Portu 1 (+) oraz Portu 2 (-).
- Nagłówek wykresu trybu symetrycznego pokazuje oddzielne wartości Portu 1 i Portu 2, a kolory jednostek odpowiadają kolorom przebiegów.
- Test interfejsu rzeczywiście pokazuje okno wykresu z podłączonym modelem danych, aby wykrywać błędy aktywacji powiązań WPF.
- Eksport napięcia, prądu albo obu wielkości do CSV.
- Obliczanie rezystancji podczas ograniczenia prądowego.
- Zapamiętywanie ostatnio wybranych portów.

Aplikacja nie zawiera trybu demonstracyjnego ani symulowanych urządzeń. Korzysta wyłącznie z rzeczywistych portów COM widocznych w systemie Windows.

## Uruchomienie

Gotowy pakiet znajduje się w wersjonowanym katalogu `artifacts/final/Korad-KA3005P-v<wersja>-win-x64`.

Uruchom:

```powershell
Korad.KA3005P.exe
```

Wybierz port COM i użyj przycisku `Offline`, aby nawiązać połączenie. Po poprawnym połączeniu przycisk zmieni opis na `Online`. Rozłączenie i zamknięcie aktywnej sesji wymusza polecenie OFF przed zwolnieniem portu.

## Komunikacja

Port jest konfigurowany jako 9600 bit/s, 8 bitów danych, brak parzystości, 1 bit stopu i DTR wyłączone. Polecenia urządzenia są wysyłane bez CR/LF.

Każdy port ma osobną, serializowaną sesję komunikacyjną działającą poza wątkiem interfejsu. Szybkie zmiany tej samej nastawy są łączone do najnowszej wartości. Polecenia wyjścia mają pierwszeństwo przed pomiarami.

Aplikacja odpytuje `VOUT1?` i `IOUT1?`. Nie wykonuje dodatkowych zapytań `VSET1?`, `ISET1?` ani `STATUS?` po każdej zmianie nastawy.

## Budowanie i testy

Wymagany jest Windows oraz SDK .NET wskazany w `global.json`.

```powershell
dotnet restore Korad.KA3005P.sln
dotnet test Korad.KA3005P.sln -c Release
dotnet build Korad.KA3005P.sln -c Release
./build.ps1 -Publish
```

Publikacja samodzielnego pakietu Windows x64:

```powershell
./build.ps1 -Publish
```

Procedura testu z fizycznymi zasilaczami znajduje się w [docs/testing-hardware.md](docs/testing-hardware.md).

## Autor i licencja

- Mateusz Skipor
- Inżynier technik elektroniki
- mskiporsklep@op.pl

Projekt jest udostępniany na warunkach PolyForm Noncommercial License 1.0.0. Pełny tekst znajduje się w [LICENSE](LICENSE), a informacje o zależnościach w [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
