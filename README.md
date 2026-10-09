# LabStation

LabStation to zestaw pięciu niezależnych aplikacji do obsługi przyrządów laboratoryjnych oraz osobny panel pełnoekranowy łączący ich funkcje bez uruchamiania aplikacji podrzędnych.

## Aplikacje

- `KA3005P` - Korad KA3005P v0.2.10
- `SDS1000CML Viewer` - Siglent SDS1000CML Viewer v0.7.4
- `SDG1000X Control` - Siglent SDG1000X Control v0.2.11
- `SDM3000 Viewer` - Siglent SDM3000 Control v0.2.4
- `SDL1000X Control` - Siglent SDL1000X Control v0.1.3

Każda aplikacja pozostaje osobnym plikiem EXE. `LabStation.exe` korzysta z tych samych paneli i logiki, ale osadza je bezpośrednio w jednym oknie. Moduły zachowują niezależne połączenia, stany i sesje.

## Stan

Gotowe aplikacje znajdują się w katalogach `KA3005P`, `SDS1000CML Viewer`, `SDG1000X Control`, `SDM3000 Viewer` i `SDL1000X Control`. Wszystkie korzystają z WPF, wspólnego języka wizualnego i paneli `UserControl`. Samodzielne pliki EXE są cienkimi powłokami tych paneli.

Pełnoekranowy panel znajduje się w `src/LabStation.App`. Wariant 1 rozmieszcza Korada na całej szerokości górnej części okna. Dolna część zawiera oscyloskop, generator oraz prawą kolumnę z multimetrem i obciążeniem. Menu modułów udostępnia konfiguracje Korada, eksporty CSV, skanowanie sieci i Auto connect zgodnie z możliwościami poszczególnych urządzeń.

Korad w panelu wspólnym ma trzy konfiguracje: `1 Single`, `2 Single` oraz `Dual`. Dwa panele Single mają niezależne sesje i nie mogą jednocześnie dzierżawić tego samego portu COM. Zmiana konfiguracji jest blokowana, gdy którekolwiek wyjście jest włączone.

Pierwsza wersja `SDL1000X Control` obsługuje statyczne tryby CC, CV, CP, CR i LED, pomiary napięcia, prądu, mocy i rezystancji, zabezpieczenia OCP/OPP oraz bezpieczne sterowanie wejściem. Została zweryfikowana na symulatorze protokołu, ponieważ fizyczne obciążenie nie było dostępne.

Wspólne elementy interfejsu znajdują się w `Shared/LabStation.UI`. Biblioteka zawiera motyw, przyciski, standard przycisków kursorów, pola tekstowe, listy rozwijane, menu i paski narzędzi, edytor liczbowy ze strzałkami, lampki stanu, wspólny wykres z powiększaniem i kursorami, obsługę motywu systemowego oraz uniwersalne menu i okna Autor/Licencja.

Wspólna komunikacja znajduje się w `Shared/LabStation.Instruments`. Obejmuje transport VXI-11 i TCP SCPI, tekstowe i binarne zapytania SCPI, parser `*IDN?`, ograniczone skanowanie lokalnych podsieci IPv4 oraz prymitywy serializacji i kolejkowania. Konkretna aplikacja nadal samodzielnie rozstrzyga, które modele obsługuje. Polecenia, parsery odpowiedzi i reguły bezpieczeństwa pozostają w aplikacjach konkretnych urządzeń.

Oscyloskop komunikuje się wyłącznie przez LAN/VXI-11. Transport USB nie jest częścią projektu.

Porównanie transportów, kolejek i protokołów Korada, Oscyloskopu i Generatora oraz wymagania komunikacyjne przyszłych aplikacji SDM3055 i SDL1020X-E opisuje [przegląd mechanizmów komunikacji](docs/communication-review.md).

Tryby demonstracyjne nie są częścią projektu. Programy łączą się wyłącznie z rzeczywistymi urządzeniami.

## Weryfikacja

```powershell
& '.\build.ps1'
& '.\build.ps1' -Publish
dotnet test .\KA3005P\Korad.KA3005P.sln -c Release
& '.\SDS1000CML Viewer\build.ps1'
& '.\SDG1000X Control\build.ps1'
& '.\SDM3000 Viewer\build.ps1'
& '.\SDL1000X Control\build.ps1'
```

Najnowsza publikacja LabStation trafia do `artifacts/final/win-x64`. Gotowy plik z numerem wersji znajduje się równolegle jako `artifacts/final/LabStation-v0.1.3-win-x64.exe`.
