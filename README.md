# LabStation

LabStation to zestaw pięciu niezależnych aplikacji do obsługi przyrządów laboratoryjnych oraz docelowy, osobny panel pełnoekranowy łączący ich funkcje bez uruchamiania aplikacji podrzędnych.

## Aplikacje

- `KA3005P` - Korad KA3005P v0.1.7
- `SDS1000CML Viewer` - Siglent SDS1000CML Viewer v0.4.1
- `SDG1000X Control` - Siglent SDG1000X Control
- `SDM3000 Viewer` - Siglent SDM3000 Viewer
- `SDL1000X Control` - Siglent SDL1000X Control

Każda aplikacja jest osobnym plikiem EXE. W przyszłym programie LabStation odpowiadające im panele będą niezależne komunikacyjnie, lecz na stałe rozmieszczone w jednym oknie.

## Stan

Gotowe aplikacje znajdują się w katalogach `KA3005P` i `SDS1000CML Viewer`. Pozostałe katalogi są uzupełniane kolejno, z zachowaniem wspólnego stylu i mechanik przeznaczonych do ponownego użycia w LabStation.

Oscyloskop komunikuje się wyłącznie przez LAN/VXI-11. Transport USB nie jest częścią projektu.

Tryby demonstracyjne nie są częścią projektu. Programy łączą się wyłącznie z rzeczywistymi urządzeniami.

## Weryfikacja

```powershell
dotnet test .\KA3005P\Korad.KA3005P.sln -c Release
& '.\SDS1000CML Viewer\build.ps1'
```

