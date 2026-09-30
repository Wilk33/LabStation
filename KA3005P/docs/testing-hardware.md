# Weryfikacja Korad KA3005P

## Zakres

Procedura dotyczy wyłącznie rzeczywistych zasilaczy dostępnych przez porty COM. Aplikacja nie zawiera trybu demonstracyjnego ani symulowanego transportu.

## Przygotowanie

1. Sprawdź fizyczne połączenia i polaryzację.
2. Przed testem odłącz obciążenie albo zastosuj bezpieczne obciążenie testowe.
3. Sprawdź, które porty COM odpowiadają zasilaczom.
4. Potwierdź, że wyjścia obu zasilaczy są wyłączone.
5. Nie wybieraj tego samego portu dla obu sesji Dual.

## Test pojedynczego zasilacza

1. Uruchom `Korad.KA3005P.exe`.
2. Pozostaw tryb `Pojedynczy`.
3. Wybierz port COM i połącz urządzenie.
4. Ustaw bezpieczne napięcie oraz ograniczenie prądu.
5. Włącz wyjście i porównaj pomiary aplikacji z wyświetlaczem zasilacza.
6. Zmień nastawę kilkoma szybkimi kliknięciami i sprawdź responsywność interfejsu.
7. Otwórz wykres i sprawdź aktualizację prądu oraz napięcia.
8. Wyłącz wyjście na kilka minut, włącz je ponownie i potwierdź, że dane pozostały na wykresie, a oś czasu nie zawiera okresu OFF.
9. Zapisz pomiary do CSV i sprawdź numer próbki oraz czas od początku sesji.
10. Wyłącz wyjście.
11. Rozłącz urządzenie i potwierdź stan OFF na zasilaczu.

## Test Dual

1. Przełącz aplikację na tryb `Dual`.
2. Wybierz dwa różne porty COM.
3. Wybierz tryb zgodny z rzeczywistym połączeniem: szeregowy, równoległy albo symetryczny.
4. Połącz oba urządzenia przy wyłączonych wyjściach.
5. Ustaw bezpieczne wartości i włącz wyjścia.
6. Wykonaj serię szybkich zmian napięcia i prądu.
7. Sprawdź, czy UI pozostaje responsywny, a oba urządzenia kończą na najnowszych wartościach.
8. Sprawdź pomiary, wykres oraz eksport CSV.
9. Wyłącz wyjścia.
10. Rozłącz sesje i niezależnie potwierdź stan OFF obu zasilaczy.

## Test awarii

1. Przy wyjściach OFF rozłącz jeden wirtualny port COM.
2. Sprawdź komunikat błędu i stan drugiej sesji.
3. Przywróć port i ponownie połącz aplikację.
4. Zamknij aplikację podczas aktywnego połączenia i potwierdź końcowy stan OFF.

## Udokumentowany test fizyczny 2026-09-25

- Dwa zasilacze były dostępne jako COM3 i COM4.
- Test obejmował połączenie obu urządzeń, ustawienie nastaw, szybkie zmiany, włączenie wyjść, pomiary i końcowe wyłączenie.
- Średni odstęp próbek wyniósł 105,6 ms na COM3 i 100,1 ms na COM4.
- Przy nastawie 1,50 V i 0,200 A na każdy zasilacz oraz zwartych wyjściach pomiar łączny wyniósł 0,00 V i 0,399 A.
- Po teście oba wyjścia ustawiono na OFF, a następnie na oba porty niezależnie wysłano `OUT0`.

Wyniki dotyczą konkretnej konfiguracji i stanu urządzeń w dniu testu. Nie są ogólną specyfikacją wszystkich egzemplarzy KA3005P.
