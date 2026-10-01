# LabStation.UI

`LabStation.UI` jest wspólną biblioteką prezentacji dla samodzielnych aplikacji przyrządów i przyszłego pełnoekranowego LabStation.

## Zakres

- Paleta tła, pól i tekstu.
- Style okien, przycisków i przycisków powtarzalnych.
- Pola tekstowe.
- Listy rozwijane i ich elementy.
- Menu, podmenu, separatory i paski narzędzi.
- Karty.
- `NumericEditor` z polem tekstowym, jednostką oraz strzałkami zwiększania i zmniejszania.
- `NumericValueEditor` o tym samym standardzie wizualnym, przeznaczony dla paneli operujących bezpośrednio na wartościach liczbowych i zdarzeniu zatwierdzenia.
- `EngineeringStepSelector` dla przycisków mnożników `G`, `M`, `k`, `1`, `m`, `u`, `n`, z możliwością ograniczenia widocznego zestawu przez `VisibleMultipliers`.
- `LabStationCursorButtonStyle` i `CursorButtonVisual` zapewniające wspólny stan przycisków kursorów: biały tekst w stanie nieaktywnym, czarny w aktywnym i biały podczas wskazania myszą.
- `LabStationStatusTextStyle` dla białego tekstu zwykłego statusu. Kolor czerwony jest ustawiany lokalnie wyłącznie dla błędu.
- Style `LabStationChannelTabControlStyle` i `LabStationChannelTabItemStyle` dla równych zakładek kanałów z kolorowym stanem aktywnym.
- `StatusLamps` zachowujący dokładny układ, kształt i kolory trzech lampek Korada dla stanu połączenia i wyjścia.
- `AboutMenuItem` z pozycjami Autor i Licencja.
- Uniwersalne okna `AuthorWindow` i `LicenseWindow`.
- `SystemTheme` dla kolorów menu i ciemnego paska tytułu Windows.
- `ApplicationPresentation` przekazujący nazwę, autora, licencję i ikonę konkretnej aplikacji.

## Granice

Biblioteka nie zna protokołów urządzeń, adresów IP, portów COM, SCPI, VXI-11, kolejek poleceń, modeli pomiarowych ani logiki bezpieczeństwa. Każda aplikacja zachowuje własny panel przyrządu oraz własną sesję i transport.

Korad wykorzystuje wspólny `NumericEditor`, `EngineeringStepSelector`, `StatusLamps`, motyw, menu, okna informacyjne i bazę wykresu `TimeSeriesPlot`. Oscyloskop wykorzystuje wspólny motyw, przyciski kursorów, menu, okna informacyjne i bazę `TimeSeriesPlot`, ale zachowuje własną obsługę dużych bloków przebiegu. Generator wykorzystuje `NumericValueEditor`, `EngineeringStepSelector`, wspólne zakładki kanałów, motyw, menu i okna informacyjne.

Kształt i kolor kontrolki są częścią jej kontraktu wizualnego. Gdy inna aplikacja potrzebuje odmiennej kontrolki, biblioteka otrzymuje dodatkowy, nazwany komponent albo styl. Nie zmienia to wyglądu istniejącego `StatusLamps` ani kontrolek swoistych dla przyrządu.
