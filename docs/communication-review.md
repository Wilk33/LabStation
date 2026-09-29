# Przegląd mechanizmów komunikacji LabStation

Stan przeglądu: 2026-09-29.

## Wniosek

Komunikacji wszystkich przyrządów nie należy łączyć w jedną klasę. Wspólne są transport, podstawowe operacje SCPI, identyfikacja urządzenia i część mechaniki serializacji. Polecenia, parsery odpowiedzi, bezpieczeństwo i polityka kolejki pozostają właściwe dla konkretnego przyrządu.

Największym rzeczywistym duplikatem jest `Vxi11Transport`. Oscyloskop i Generator mają dwie semantycznie równoważne implementacje XDR, ONC RPC, portmappera, tworzenia łącza `inst0`, dzielenia zapisu, wieloczęściowego odczytu, limitu 32 MiB i zamykania łącza. Różnią się głównie przestrzenią nazw, formatowaniem i tekstem błędów.

Docelową granicą powinien być wspólny projekt `Shared/LabStation.Instruments`, niezależny od WPF i od `LabStation.UI`.

## Elementy wspólne

### Transport

- `IInstrumentTransport` z operacjami zapisu, zapytania i zamknięcia.
- Jedna implementacja `Vxi11Transport` dla przyrządów SIGLENT pracujących przez LAN/VXI-11.
- Wspólne kodowanie XDR i obsługa ONC RPC.
- Konfigurowalne limity czasu, maksymalny rozmiar odpowiedzi, nazwa urządzenia VXI-11 oraz terminator poleceń.
- Opcjonalny `TcpScpiTransport` dla urządzeń, które rzeczywiście udostępniają zwykły socket SCPI. Nie może zastąpić VXI-11 w SDS1102CML+.

### Podstawowe SCPI

- zapytanie tekstowe ASCII z usunięciem terminatora,
- zapytanie binarne bez konwersji do tekstu,
- bezpieczne dodawanie `\n`,
- parser odpowiedzi `*IDN?`,
- wspólne błędy transportu i przekroczenia czasu,
- pomocnicze parsowanie liczb SCPI i wartości inżynierskich.

Walidacja producenta, modelu i wersji pozostaje w kliencie konkretnego urządzenia. Wspólny parser może rozbić `*IDN?`, ale nie może decydować, czy aplikacja Generatora akceptuje dany model.

### Serializacja pracy

Każdy przyrząd nadal potrzebuje jednego właściciela transportu i jednej uporządkowanej ścieżki wejścia-wyjścia. Można współdzielić prymitywy, ale nie jedną sztywną politykę kolejki:

- operacje uporządkowane,
- operacje priorytetowe,
- najnowsza wartość dla danego klucza,
- operacje okresowe wykonywane tylko wtedy, gdy nie czeka polecenie użytkownika,
- anulowanie oczekujących operacji podczas zamykania,
- zakończenie transportu dopiero po ustaniu aktywnego wejścia-wyjścia.

## Różnice, które muszą pozostać lokalne

### Korad KA3005P

- transport COM 9600/8/N/1 bez terminatora,
- minimalny odstęp 45 ms pomiędzy komendami,
- kolejność zamknięcie, wyjście, nastawy, pomiar,
- `OUT0` ma znaczenie bezpieczeństwa i usuwa oczekujące nastawy,
- dwa niezależne zasilacze w trybie Dual.

Korad może współdzielić ogólne prymitywy kolejki, ale nie transport VXI-11 ani protokół SCPI.

### Oscyloskop SDS1102CML+

- duże odpowiedzi binarne i dekodowanie `WAVEDESC`,
- odczyt podglądu może zostać pominięty, gdy czeka polecenie użytkownika,
- ręczne polecenia muszą czekać za aktywnym transferem,
- odczyt nie może automatycznie zmieniać stanu akwizycji ani kanałów.

### Generator SDG1032X

- kolejka priorytetowa, uporządkowana i `latest-wins` dla szybkich zmian nastaw,
- dwa kanały z własnymi poleceniami `C1:` i `C2:`,
- parsery `BSWV?`, `OUTP?` i funkcji generatora,
- zmiana wyjścia ma inny kontrakt bezpieczeństwa niż podgląd oscyloskopu.

### Multimetr SDM3055

Oficjalna instrukcja opisuje LAN przez VXI-11, sockety i Telnet. Socket SCPI używa portu 5025 i terminatora `\n`. Dla spójności z pozostałymi przyrządami SIGLENT podstawowym transportem może być wspólny VXI-11, natomiast socket 5025 może pozostać dodatkowym transportem.

Mechanika pomiarowa wymaga własnej sesji:

- `CONFigure` wybiera funkcję i parametry pomiaru,
- `READ?` inicjuje pomiar, zwraca wyniki i usuwa je z pamięci odczytów,
- `FETCh?` zwraca wyniki bez ich usuwania,
- `DATA:LAST?` zwraca ostatni pomiar,
- zmiana konfiguracji, `INITiate`, `MEASure:<function>?`, `READ?`, `*RST` i `SYSTem:PRESet` czyszczą pamięć odczytów,
- przeciążenie lub obwód otwarty może być zwracany jako wartość około `9.9E37`; aplikacja musi prezentować jawny stan przeciążenia, a nie zwykłą liczbę,
- funkcje obejmują między innymi napięcie i prąd AC/DC, rezystancję 2- i 4-przewodową, częstotliwość, okres, temperaturę, pojemność, ciągłość i test diody.

Multimetr pasuje do wspólnego transportu i serializacji, ale jego wyzwalanie, pamięć odczytów i interpretacja przeciążenia są lokalne.

### Obciążenie SDL1020X-E

Dokumentacja serii SDL1000X opisuje połączenie LAN przez zasób `TCPIP0::<IP>::INSTR`, a instrukcja serwisowa wskazuje VXI-11. Obciążenie może więc używać tego samego transportu VXI-11 co Oscyloskop i Generator.

Własna sesja musi obsługiwać między innymi:

- statyczne tryby CC, CV, CP, CR i LED,
- stan wejścia `:SOURce:INPut:STATe?` oraz jawne ON/OFF,
- nastawy trybu i zakresów,
- odczyt napięcia, prądu, mocy i rezystancji,
- odczyt 200 punktów danych trendu przez `MEASure:WAVEdata?`,
- tryby dynamiczne, List i Program,
- zabezpieczenia OCP, OPP, OVP, OTP i pozostałe mechanizmy ochronne urządzenia.

Włączenie wejścia obciążenia jest operacją niebezpieczną. Zapytanie o bieżący stan, priorytetowe OFF, kontrola limitów oraz polityka odłączenia muszą pozostać jawne w warstwie SDL, a nie być ukryte w ogólnej bibliotece SCPI.

## Zalecany podział kodu

```text
Shared/LabStation.Instruments
|- Transport/IInstrumentTransport
|- Transport/Vxi11Transport
|- Transport/TcpScpiTransport
|- Scpi/ScpiConnection
|- Scpi/ScpiIdentity
|- Scheduling/SerializedOperationGate
|- Scheduling/LatestRequestQueue

SDS1000CML Viewer/Scope.Core
|- ScopeClient
|- Waveform i parser WAVEDESC
|- polityka podglądu i poleceń

SDG1000X Control/Sdg1032X.Core
|- SiglentGeneratorClient
|- GeneratorSession
|- parsery generatora

SDM3000 Viewer
|- MultimeterClient i MultimeterSession
|- konfiguracja, wyzwalanie, pamięć i przeciążenie

SDL1000X Control
|- ElectronicLoadClient i ElectronicLoadSession
|- tryby, nastawy, zabezpieczenia i bezpieczny stan wejścia
```

Pierwszy bezpieczny refaktor powinien przenieść bez zmian zachowania interfejs transportu, VXI-11 i jego istniejący test z lokalnym serwerem RPC. Dopiero po przejściu testów Oscyloskopu i Generatora warto wydzielić wspólne prymitywy kolejkowania. Próba utworzenia od razu jednej uniwersalnej sesji ukryłaby istotne różnice bezpieczeństwa i kolejności operacji.

## Źródła

- Kod Oscyloskopu: `SDS1000CML Viewer/src/Scope.Core`.
- Kod Generatora: `D:\Programowanie\Projekty\Sig. SDG 1032X\src\Sdg1032X.Core`.
- Kod Korada: `KA3005P/src/Ka3005P.Core`.
- [SIGLENT - dokumentacja multimetrów i SDM Series Programming Guide](https://www.siglenteu.com/resources/documents/digital-multimeter/)
- [SIGLENT SDM Series Programming Guide EN02A](https://www.siglenteu.com/download/2581/)
- [SIGLENT SDM3055 User Manual](https://www.siglenteu.com/download/2567/)
- [SIGLENT - dokumentacja obciążeń SDL1000X](https://www.siglenteu.com/resources/documents/dc-electronic-load/)
- [SIGLENT SDL1000X Programming Guide E02B](https://www.siglenteu.com/wp-content/uploads/dlm_uploads/2023/10/SDL1000X_programming_guide_E02B.pdf)
- [SIGLENT SDL1000X Service Manual](https://int.siglent.com/u_file/download/23_04_12/SDL1000X%20Series%20Service%20Manual_E01B.pdf)
