# SDL1000X Control - projekt pierwszej wersji

## Cel

Powstaje piata niezalezna aplikacja LabStation: `Siglent SDL1000X Control` dla obciazenia elektronicznego SDL1020X-E. Interfejs wdraza wybrany wariant 3 - panel nastawczy o stalym rozmiarze okolo 520 x 400 px, bez wykresu, przeznaczony rowniez do pozniejszego osadzenia jako `UserControl` w LabStation.

## Zakres funkcjonalny

- polaczenie LAN/VXI-11, identyfikacja przez `*IDN?`, skan sieci i zapamietywane `Auto connect`,
- statyczne tryby CC, CV, CP, CR i LED,
- odczyt napiecia, pradu, mocy i rezystancji,
- odczyt stanu wejscia oraz jawne ON/OFF,
- nastawa wlasciwa dla aktywnego trybu; LED udostepnia `Vo`, `Io` i `Rco`,
- edytowalne progi OCP i OPP wraz z wlaczeniem zabezpieczen,
- OVP i OTP jako pola informacyjne, poniewaz przewodnik SCPI nie dokumentuje komend ustawiajacych ich progi,
- automatyczne odswiezanie stanu bez nadpisywania edytowanej wartosci,
- wspolny styl, menu Autor/Licencja, stale okno z minimalizacja i wielorozmiarowa ikona.

## Bezpieczenstwo

- nawiazanie polaczenia nigdy nie wlacza wejscia,
- kazdy zapis trybu, nastawy lub zabezpieczenia najpierw odczytuje stan wejscia i jest odrzucany, gdy wejscie jest ON,
- ON wymaga bezposredniego klikniecia i potwierdzenia pokazujacego tryb oraz nastawy,
- OFF ma pierwszenstwo przed oczekujacymi nastawami,
- rozlaczenie i zamkniecie probuja ustawic OFF i zweryfikowac stan przed zamknieciem transportu,
- liczby musza byc skonczone, nieujemne i miescic sie w limitach modelu 150 V, 30 A i 200 W.

## Architektura

- `Sdl1000X.Core` zawiera modele, parser i generator polecen SCPI, klienta urzadzenia oraz asynchroniczna sesje z pojedynczym wlascicielem transportu.
- `Sdl1000X.App` zawiera cienkie okno i panel `ElectronicLoadView`, ktory moze zostac ponownie uzyty w LabStation.
- `Sdl1000X.Tests` zawiera deterministyczny symulator SDL1020X-E na granicy transportu oraz testy protokolu, bledow, bezpieczenstwa, kolejkowania, ustawien i ukladu WPF.
- Transport, skanowanie i wyglad pochodza z `Shared/LabStation.Instruments` oraz `Shared/LabStation.UI`.

## Granice pierwszej wersji

Pierwsza wersja nie zawiera wykresu, trybow dynamicznych, List, Program, Battery, OCPT ani OPPT. Brak fizycznego urzadzenia jest jawna granica weryfikacji - zgodnosc z rzeczywistym firmware pozostaje do potwierdzenia po uzyskaniu SDL1020X-E.

## Zrodla techniczne

- SIGLENT SDL1000X Programming Guide E02B: https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2023/10/SDL1000X_programming_guide_E02B.pdf
- SIGLENT SDL1000X Series: https://www.siglenteu.com/dc-electronic-load/sdl1000x/
