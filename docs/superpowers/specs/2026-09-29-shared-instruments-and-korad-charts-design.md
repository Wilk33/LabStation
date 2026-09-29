# Wspólna komunikacja i wykresy Korada - projekt

## Cel

LabStation ma dostarczać jedną bibliotekę transportu i podstawowych mechanizmów komunikacji dla przyrządów sieciowych, bez tworzenia jednej uniwersalnej sesji urządzenia. Korad ma otrzymać wykres oparty na tym samym współdzielonym silniku wizualnym co Oscyloskop, z dwiema aktywnymi po wyłączeniu wyjścia liniami kursora, przybliżaniem oraz historią do 20 480 próbek.

## Granice komunikacji

- `Shared/LabStation.Instruments` nie zależy od WPF.
- Wspólne są `IInstrumentTransport`, VXI-11, bezpośredni socket SCPI, tekstowe i binarne zapytania SCPI, parser `*IDN?`, serializacja operacji oraz kolejka priorytetowa/latest-wins.
- Oscyloskop i Generator korzystają z jednej implementacji VXI-11.
- Klienci konkretnego urządzenia nadal odpowiadają za akceptowany model, komendy, parsery odpowiedzi, bezpieczeństwo i politykę kolejki.
- Korad pozostaje przy transporcie COM 9600/8/N/1 i własnej kolejce z bezpiecznym priorytetem OUT0.

## Generator

Generator zostaje przeniesiony do `SDG1000X Control` jako trzecia samodzielna aplikacja LabStation. Zachowuje działającą obsługę SDG1032X, ale korzysta ze wspólnych bibliotek komunikacji i UI. Nie zawiera trybu demonstracyjnego. Nadal buduje osobny EXE.

## Wspólny wykres

`Shared/LabStation.UI` udostępnia wykres szeregów czasowych z:

- ciemnym tłem i siatką zgodną z wykresem Oscyloskopu,
- osiami i jednostkami inżynierskimi,
- skalowaniem dużej liczby punktów,
- przybliżaniem kółkiem myszy,
- konfigurowalną liczbą kursorów,
- odczytem wartości wszystkich widocznych szeregów w pozycji kursora,
- różnicą pomiędzy parą kursorów.

Oscyloskop używa czterech kursorów. Korad używa dwóch, widocznych i aktywnych wyłącznie wtedy, gdy połączony zasilacz lub para zasilaczy ma wyjście w stanie OFF. Domyślna wysokość okna wykresu Korada wynosi 360 px, tyle samo co główne okno trybu Single.

## Dane Korada

- Historia wykresu przechowuje maksymalnie 20 480 najnowszych próbek.
- Tryby Single, szeregowy i równoległy pokazują wynik logiczny jak dotychczas.
- Tryb symetryczny pokazuje oddzielne szeregi Portu 1 jako strony dodatniej i Portu 2 jako strony ujemnej.
- W trybie symetrycznym Port 1 ma dodatni znak napięcia i prądu, a Port 2 ujemny.
- Główne okno Dual pokazuje oba podpisane zestawy danych, nie tylko wartość zagregowaną.

## Ikony i publikacja

Pary ICO/PNG dla Oscyloskopu, Multimetru i Obciążenia pozostają w katalogach ich aplikacji. Generator zachowuje własną parę ikon. Zmiany źródłowe i dokumentacja zostają opublikowane w Git, natomiast binarne zasoby GitHub Release wymagają osobnej zgody.

## Weryfikacja

- testy protokołu VXI-11 na lokalnym serwerze RPC,
- testy socketu SCPI i parsera identyfikacji,
- testy obu polityk kolejkowania,
- istniejące testy Oscyloskopu i Generatora po migracji,
- testy 20 480 próbek, kursorów OFF, zoomu i danych symetrycznych Korada,
- test rzeczywistego układu WPF,
- kompilacja Release, publikacja samodzielnych EXE i uruchomienie bez łączenia ze sprzętem.

