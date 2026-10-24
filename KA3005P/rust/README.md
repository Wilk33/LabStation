# Korad - migracja logiki sterowania do Rust

Ten katalog zawiera działającą bibliotekę komunikacji COM oraz lokalny serwer sterowania dla zasilaczy Korad. Binarny `korad-rs.exe` obsługuje rzeczywiste porty COM i nie ma trybu demonstracyjnego. Interfejs okienkowy oraz panel Korada w LabStation nadal używają dotychczasowego kodu WPF. Ten etap nie jest jeszcze zamiennikiem tych dwóch interfejsów.

## Uruchomienie

Wymagany jest Rust dla Windows x64 z narzędziami MSVC. Z katalogu `KA3005P/rust`:

```powershell
cargo test
cargo build --release
./target/release/korad-rs.exe ports
./target/release/korad-rs.exe probe COM3
./target/release/korad-rs.exe serve
```

`probe` wykonuje wyłącznie odczyt identyfikacji, nastaw, pomiaru i stanu. `serve` podaje lokalny adres API oraz trzy losowe tokeny. Tokeny nie są zapisywane na dysku. Polecenie `quit` zamyka serwer i próbuje wysłać `OUT0` do każdego połączonego zasilacza. Nie należy przekazywać tokenów do logów lub repozytorium.

API nasłuchuje tylko na `127.0.0.1` na losowym porcie. Nagłówek każdego żądania: `Authorization: Bearer <token>`. Żądania i odpowiedzi są w JSON. Maksymalny rozmiar żądania wynosi 16 KiB.

## Role i uprawnienia

| Rola | Zakres |
| --- | --- |
| `Observer` | Lista portów, podłączonych sesji, odczyt stanu, identyfikacja i pomiary. |
| `Operator` | Powyższe oraz połączenie, rozłączenie, ON/OFF, napięcie i prąd z limitem 12,50 V / 1,500 A na każdy fizyczny zasilacz. |
| `Expert` | Pełne 31,00 V / 5,100 A na fizyczny zasilacz oraz `BEEP`, `OCP`, `OVP`, `SAV`, `RCL`. `RCL`, `OCP0` i `OVP0` wymagają potwierdzonego stanu OFF. |

Tokeny są niezależne. Komenda bez pasującej roli lub poza limitem jest odrzucana przed dotknięciem portu. API nie przyjmuje dowolnych ciągów SCPI ani surowych bajtów.

## Kontrakt HTTP

| Metoda i adres | Treść | Wynik |
| --- | --- | --- |
| `GET /v1/ports` | Brak | Dostępne porty COM. |
| `GET /v1/devices` | Brak | Połączone porty. |
| `GET /v1/state?port=COM3` | Brak | Ostatni stan sesji. |
| `POST /v1/connect` | `{"port":"COM3"}` | Otwiera port i odczytuje `STATUS?`. |
| `POST /v1/disconnect` | `{"port":"COM3"}` | Próbuje `OUT0` i zwalnia port. |
| `POST /v1/poll` | `{"port":"COM3"}` | Odczytuje `VOUT1?`, `IOUT1?`, `STATUS?`. |
| `POST /v1/command` | `{"port":"COM3","command":"set_voltage","value":1200}` | Wykonuje typowaną komendę. |
| `POST /v1/dual/setpoints` | `{"first":"COM3","second":"COM4","mode":"series","voltage_hundredths":2400,"current_thousandths":1000}` | Przelicza nastawy i stosuje je do obu portów. |
| `POST /v1/dual/output` | `{"first":"COM3","second":"COM4","enabled":true}` | Steruje oboma wyjściami; przy częściowym błędzie ON próbuje OFF na obu. |

Nazwy `command`: `set_voltage`, `set_current`, `output_on`, `output_off`, `beep_on`, `beep_off`, `ocp_on`, `ocp_off`, `ovp_on`, `ovp_off`, `save`, `recall`, `read_voltage`, `read_current`, `read_set_voltage`, `read_set_current`, `read_status`, `identify`. Dla `save` i `recall` należy podać `bank` od 1 do 5. Dla nastaw `value` oznacza setne wolta albo tysięczne ampera, bez liczby zmiennoprzecinkowej. Na przykład `1200` to 12,00 V, a `1500` to 1,500 A. Tryby Dual: `series`, `parallel`, `symmetric`.

Stan odróżnia wartości wysłane od odczytanych. Po `OUT1` lub `OUT0` silnik automatycznie odpytuje `STATUS?` i zwraca sukces dopiero po potwierdzeniu oczekiwanego stanu wyjścia. Błąd komunikacji unieważnia wiedzę o stanie wyjścia (`null`). Kod błędu ma postać `TOUT`, `PORT`, `DATA` lub `STATE`, a szczegółowy opis jest osobnym polem. Przed `OUT1` roli Operator odczytywane są rzeczywiste nastawy napięcia i prądu - nie wystarczy, że wcześniejsze żądania API były w limicie.

## Komunikacja i test sprzętowy

Port jest otwierany jako 9600/8/N/1 bez DTR, a polecenia nie mają terminatora. Zachowany jest odstęp co najmniej 45 ms między zapisami do jednego portu. Odpowiedzi liczbowe i bajt statusu muszą mieć kompletne ramki, a całe polecenie ma limit czasu 1500 ms. Sesje należą do jednego procesu, dostęp do portów i operacji Dual jest serializowany. Odczyt nie jest wykonywany samoczynnie - klient wywołuje `/v1/poll`.

Skrypt `tests/hardware-smoke.ps1` przeznaczony jest dla dwóch rzeczywistych zasilaczy na COM3 i COM4. Przed ON sprawdza OFF i nastawy nie większe niż 12,50 V / 1,500 A. Przez zadany czas zmienia nastawy o mały krok, odpytuje oba zasilacze, następnie wyłącza wyjścia, przywraca pierwotne nastawy i niezależnie sprawdza `STATUS?` po zamknięciu serwera. Domyślny czas to 120 s. Skrypt należy uruchamiać wyłącznie świadomie, gdy fizyczne połączenia odpowiadają temu układowi.

W testach bieżącego sprzętu `*IDN?` zwróciło `S-LS-31 V2.0` na COM3 i `S-LS-31 V5.8 SN:03694911` na COM4, mimo że aplikacja obsługuje zasilacze opisane jako KA3005P. Identyfikacja nie jest więc jedynym kryterium zgodności. Rozszerzonych poleceń `SAV`, `RCL`, `OCP` i `OVP` nie należy uznawać za zweryfikowane na tych egzemplarzach tylko na podstawie identyfikacji lub testów atrap.

Kontrola z 2026-10-24: po poprawkach bezpieczeństwa i HTTP dwuminutowy przebieg na COM3 i COM4 zebrał 188 odczytów bez błędu, z cykliczną niewielką zmianą nastaw przy włączonych wyjściach. Po przywróceniu nastaw odczyty bezpośrednie potwierdziły COM3 = 4,21 V / 0,100 A i COM4 = 3,30 V / 1,102 A oraz OFF na obu portach. Jest to potwierdzenie tych konkretnych operacji na tych egzemplarzach, nie wszystkich poleceń rozszerzonych.

Podstawą zestawu poleceń jest [KA Series Single Channel Remote Control Syntax V2.0](https://github.com/DudeYarvie/Korad-KA3005P-PySerial-Example) i [otwarta implementacja Rust ka3005p](https://github.com/Nicoretti/ka3005p). Kod transportu napisano tu odrębnie, aby zachować parametry sprawdzone we wcześniejszej aplikacji, w tym 45 ms między poleceniami i obsługę wirtualnych portów COM.
