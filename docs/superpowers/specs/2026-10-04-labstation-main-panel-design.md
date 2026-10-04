# LabStation - projekt głównego panelu

## Cel

LabStation jest osobną, pełnoekranową aplikacją WPF. Osadza pięć istniejących paneli urządzeń w jednym procesie, ale nie łączy ich sesji komunikacyjnych ani stanów. Każdy panel zachowuje możliwość niezależnego połączenia, rozłączenia, skanowania i pracy.

## Układ

Wybrany jest wariant 1:

- górny pas na całą szerokość zajmuje Korad,
- lewa część pasa Korada zawiera sterowanie, a prawa wbudowany wykres,
- dolna część dzieli się na Oscyloskop, Generator oraz prawą kolumnę,
- prawa kolumna zawiera Multimetr nad Obciążeniem,
- okno startuje zmaksymalizowane, pozwala na minimalizację i nie służy do ręcznego układania paneli.

Proporcje dolnej części wynoszą około 53% dla Oscyloskopu, 18% dla Generatora i 29% dla prawej kolumny. Multimetr zajmuje około 40% wysokości prawej kolumny, a Obciążenie pozostałe 60%.

## Korad

Panel Korada udostępnia trzy wzajemnie wykluczające się konfiguracje:

- `1 Single` - jeden niezależny zasilacz i jeden wykres,
- `2 Single` - dwa niezależne zasilacze i dwa wykresy we wspólnym obszarze,
- `Dual` - dwa zasilacze kontrolowane jako układ Dual i jeden wykres wielokanałowy.

Zmiana konfiguracji jest dostępna wyłącznie przy wyłączonych wyjściach. Przełączenie zamyka bieżące sesje, zapamiętuje porty i tworzy nowy układ, korzystając ze wspólnego rejestru dzierżaw portów COM. Dla dwóch niezależnych zasilaczy sterowanie jest ułożone w dwóch zwartych kartach, a wykresy dzielą prawą część po połowie.

Eksport Korada obejmuje napięcie, prąd albo napięcie i prąd. Dla `2 Single` jeden wybór ścieżki tworzy dwa jednoznacznie oznaczone pliki P1 i P2.

## Menu modułów

Główne menu zawiera `Korad`, `Oscyloskop`, `Generator`, `Multimetr`, `Obciążenie` i `O aplikacji`.

- Korad: konfiguracja `1 Single`, `2 Single`, `Dual` oraz trzy warianty CSV.
- Oscyloskop: skanowanie, `Auto connect`, zapis CH1, CH2 albo CH1 i CH2.
- Generator: skanowanie i `Auto connect`.
- Multimetr: skanowanie, `Auto connect` i zapis pomiarów.
- Obciążenie: skanowanie i `Auto connect`.

Pozycje menu wywołują publiczne API odpowiedniego osadzonego panelu. Nie istnieje wspólny stan połączenia ani skanowanie wszystkich urządzeń jednym poleceniem.

## Cykl życia

Zamykanie LabStation kolejno zatrzymuje Korad i wszystkie cztery panele sieciowe. Aktywne zadania są anulowane, wyjścia i wejście obciążenia korzystają z istniejących bezpiecznych procedur zamknięcia, a okno zamyka się dopiero po zakończeniu zwalniania zasobów.

## Zakres pierwszej wersji

- brak dokowania, przeciągania i zmiany wielkości poszczególnych paneli,
- brak trybów demonstracyjnych,
- brak wspólnego przycisku łączenia lub skanowania,
- brak zmian protokołów i reguł bezpieczeństwa gotowych aplikacji,
- wspólny EXE jest publikowany do `artifacts/final/win-x64`.

## Weryfikacja

- test logiki konfiguracji Korada i bezpiecznego przełączania,
- test nazw plików eksportu dla dwóch niezależnych zasilaczy,
- test dyspozycji paneli i menu głównego okna,
- test niezależnego przekazywania poleceń menu do modułów,
- pełne istniejące zestawy testów pięciu aplikacji,
- kompilacja Release, publikacja `win-x64`, uruchomienie pakietu i kontrola wizualna renderu okna.
