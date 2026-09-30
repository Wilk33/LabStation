# LabStation

LabStation to zestaw pięciu niezależnych aplikacji do obsługi przyrządów laboratoryjnych oraz docelowy, osobny panel pełnoekranowy łączący ich funkcje bez uruchamiania aplikacji podrzędnych.

## Aplikacje

- `KA3005P` - Korad KA3005P v0.2.6
- `SDS1000CML Viewer` - Siglent SDS1000CML Viewer v0.7.1
- `SDG1000X Control` - Siglent SDG1000X Control v0.2.1
- `SDM3000 Viewer` - Siglent SDM3000 Viewer
- `SDL1000X Control` - Siglent SDL1000X Control

Każda aplikacja jest osobnym plikiem EXE. W przyszłym programie LabStation odpowiadające im panele będą niezależne komunikacyjnie, lecz na stałe rozmieszczone w jednym oknie.

## Stan

Gotowe aplikacje znajdują się w katalogach `KA3005P`, `SDS1000CML Viewer` i `SDG1000X Control`. Wszystkie korzystają z WPF, wspólnego języka wizualnego i paneli `UserControl` przeznaczonych do bezpośredniego użycia w przyszłym oknie LabStation. Samodzielne pliki EXE są cienkimi powłokami tych paneli.

Wspólne elementy interfejsu znajdują się w `Shared/LabStation.UI`. Biblioteka zawiera motyw, przyciski, standard przycisków kursorów, pola tekstowe, listy rozwijane, menu i paski narzędzi, edytor liczbowy ze strzałkami, lampki stanu, wspólny wykres z powiększaniem i kursorami, obsługę motywu systemowego oraz uniwersalne menu i okna Autor/Licencja.

Wspólna komunikacja znajduje się w `Shared/LabStation.Instruments`. Obejmuje transport VXI-11 i TCP SCPI, tekstowe i binarne zapytania SCPI, parser `*IDN?`, ograniczone skanowanie lokalnych podsieci IPv4 oraz prymitywy serializacji i kolejkowania. Konkretna aplikacja nadal samodzielnie rozstrzyga, które modele obsługuje. Polecenia, parsery odpowiedzi i reguły bezpieczeństwa pozostają w aplikacjach konkretnych urządzeń.

Oscyloskop komunikuje się wyłącznie przez LAN/VXI-11. Transport USB nie jest częścią projektu.

Porównanie transportów, kolejek i protokołów Korada, Oscyloskopu i Generatora oraz wymagania komunikacyjne przyszłych aplikacji SDM3055 i SDL1020X-E opisuje [przegląd mechanizmów komunikacji](docs/communication-review.md).

Tryby demonstracyjne nie są częścią projektu. Programy łączą się wyłącznie z rzeczywistymi urządzeniami.

## Weryfikacja

```powershell
dotnet test .\KA3005P\Korad.KA3005P.sln -c Release
& '.\SDS1000CML Viewer\build.ps1'
& '.\SDG1000X Control\build.ps1'
```
