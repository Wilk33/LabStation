# Siglent SDL1000X Control v0.1.2

Samodzielna aplikacja Windows oraz panel wielokrotnego użytku do sterowania obciążeniem elektronicznym SIGLENT SDL1020X-E przez LAN/VXI-11.

Pierwsza wersja obejmuje statyczne tryby CC, CV, CP, CR i LED, odczyt napięcia, prądu, mocy i rezystancji, bezpieczne sterowanie wejściem oraz udokumentowane przez producenta nastawy OCP i OPP. Nie zawiera wykresu ani trybów Dynamic, List, Program, Battery, OCPT i OPPT.

Urządzenie fizyczne nie było dostępne podczas tworzenia tej wersji. Zachowanie protokołu zostało sprawdzone przy użyciu deterministycznego symulatora SDL1020X-E oraz dokumentacji producenta.

## Budowanie

```powershell
& '.\build.ps1'
& '.\build.ps1' -Publish
```

Publikacja Windows x64 trafia do `artifacts/final/win-x64`.

## Źródła

- SIGLENT SDL1000X Programming Guide E02B: https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2023/10/SDL1000X_programming_guide_E02B.pdf
- SIGLENT SDL1000X Series Data Sheet: https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2024/05/SDL1000X_DataSheet_DS0801X-E01F.pdf
- SIGLENT SDL1000X Series: https://www.siglenteu.com/dc-electronic-load/sdl1000x/
