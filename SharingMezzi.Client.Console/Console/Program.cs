using System.Text.Json;
using SharingMezzi.Core.DTOs;
using System.Net.Http.Headers;

namespace SharingMezzi.Client.Console
{
    class Program
    {
        private static readonly HttpClient httpClient = new();
        private static readonly string API_BASE = "http://localhost:5000/api";
        private static string? authToken = null;
        private static UtenteDto? currentUser = null;

        static async Task Main(string[] args)
        {
            System.Console.Clear();
            System.Console.WriteLine("=== SharingMezzi - Sistema di Gestione Mezzi ===");
            System.Console.WriteLine();

            httpClient.DefaultRequestHeaders.Add("User-Agent", "SharingMezzi-Client/1.0");

            while (true)
            {
                if (authToken == null)
                {
                    await ShowLoginMenu();
                }
                else
                {
                    await ShowMainMenu();
                }
            }
        }

        // === MENU MANAGEMENT ===
        static async Task ShowLoginMenu()
        {
            System.Console.Clear();
            System.Console.WriteLine("=== ACCESSO SISTEMA === ");
            System.Console.WriteLine();
            System.Console.WriteLine("1. Accedi");
            System.Console.WriteLine("2. Registrati");
            System.Console.WriteLine("3. Info Sistema");
            System.Console.WriteLine("0. Esci");
            System.Console.WriteLine();
            System.Console.Write("Scegli un'opzione: ");

            var choice = System.Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await Login();
                        break;
                    case "2":
                        System.Console.WriteLine("Registrazione non implementata in questa demo");
                        await WaitKey();
                        break;
                    case "3":
                        await StatusSistema();
                        break;
                    case "0":
                        System.Console.WriteLine("Arrivederci!");
                        Environment.Exit(0);
                        break;
                    default:
                        System.Console.WriteLine("Opzione non valida");
                        await WaitKey();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
                await WaitKey();
            }
        }

        static async Task ShowMainMenu()
        {
            System.Console.Clear();
            System.Console.WriteLine($"=== BENVENUTO {currentUser?.Nome?.ToUpper()} === Credito: €{currentUser?.Credito:F2}");
            System.Console.WriteLine();
            System.Console.WriteLine("1. Visualizza mezzi disponibili");
            System.Console.WriteLine("2. Visualizza parcheggi");
            System.Console.WriteLine("3. Inizia corsa");
            System.Console.WriteLine("4. Termina corsa");
            System.Console.WriteLine("5. Storico corse");
            System.Console.WriteLine("6. Ricarica credito");
            System.Console.WriteLine("7. Test comandi MQTT");
            System.Console.WriteLine("8. Status sistema");
            System.Console.WriteLine("9. Profilo utente");
            System.Console.WriteLine("0. Logout");
            System.Console.WriteLine();
            System.Console.Write("Scegli un'opzione: ");

            var choice = System.Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await ListMezziDisponibili();
                        break;
                    case "2":
                        await ListParcheggi();
                        break;
                    case "3":
                        await IniziaCorsa();
                        break;
                    case "4":
                        await TerminaCorsa();
                        break;
                    case "5":
                        await StoricoCorse();
                        break;
                    case "6":
                        await RicaricaCredito();
                        break;
                    case "7":
                        await TestMqttCommands();
                        break;
                    case "8":
                        await StatusSistema();
                        break;
                    case "9":
                        await ProfiloUtente();
                        break;
                    case "0":
                        await Logout();
                        break;
                    default:
                        System.Console.WriteLine("Opzione non valida");
                        await WaitKey();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
                await WaitKey();
            }
        }

        // === AUTHENTICATION ===
        static async Task Login()
        {
            System.Console.Clear();
            System.Console.WriteLine("=== LOGIN === ");
            System.Console.WriteLine();

            System.Console.Write("Email: ");
            var email = System.Console.ReadLine();

            System.Console.Write("Password: ");
            var password = ReadPassword();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                System.Console.WriteLine("Email e password sono obbligatori");
                await WaitKey();
                return;
            }

            var loginRequest = new LoginDto { Email = email, Password = password };
            var json = JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"{API_BASE}/auth/login", content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = JsonSerializer.Deserialize<AuthResultDto>(result, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    if (loginResponse?.Success == true)
                    {
                        authToken = loginResponse.Token;
                        currentUser = loginResponse.User;

                        // Aggiorna header di autorizzazione
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                        System.Console.WriteLine($"Login effettuato con successo! Benvenuto {currentUser?.Nome}");
                        await WaitKey();
                    }
                    else
                    {
                        System.Console.WriteLine($"Login fallito: {loginResponse?.Message}");
                        await WaitKey();
                    }
                }
                else
                {
                    System.Console.WriteLine($"Login fallito: {result}");
                    await WaitKey();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore durante il login: {ex.Message}");
                await WaitKey();
            }
        }

        static async Task Logout()
        {
            authToken = null;
            currentUser = null;
            httpClient.DefaultRequestHeaders.Authorization = null;
            System.Console.WriteLine("👋 Logout effettuato con successo!");
            await WaitKey();
        }

        static string ReadPassword()
        {
            var password = "";
            ConsoleKeyInfo key;
            do
            {
                key = System.Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    System.Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password[0..^1];
                    System.Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            System.Console.WriteLine();
            return password;
        }

        // === MAIN FEATURES ===
        static async Task ListMezziDisponibili()
        {
            System.Console.Clear();
            await MostraMezziDisponibili();
            await WaitKey();
        }

        static async Task MostraMezziDisponibili()
        {
            System.Console.WriteLine("=== MEZZI DISPONIBILI === ");
            System.Console.WriteLine();

            try
            {
                var response = await httpClient.GetAsync($"{API_BASE}/mezzi/disponibili");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Console.WriteLine($"Errore nel recuperare i mezzi: {json}");
                    return;
                }

                var mezzi = JsonSerializer.Deserialize<MezzoDto[]>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (mezzi?.Length > 0)
                {
                    System.Console.WriteLine("┌─────┬─────────────────┬──────────┬───────────┬──────────┐");
                    System.Console.WriteLine("│ ID  │ Modello         │ Tipo     │ Tariffa   │ Batteria │");
                    System.Console.WriteLine("├─────┼─────────────────┼──────────┼───────────┼──────────┤");

                    foreach (var mezzo in mezzi)
                    {
                        var battery = mezzo.IsElettrico ? $"{mezzo.LivelloBatteria}%" : "N/A";
                        var tariffa = $"€{mezzo.TariffaPerMinuto:F2}/min";
                        
                        System.Console.WriteLine($"│ {mezzo.Id,-3} │ {mezzo.Modello,-15} │ {mezzo.Tipo,-8} │ {tariffa,-9} │ {battery,-8} │");
                    }
                    System.Console.WriteLine("└─────┴─────────────────┴──────────┴───────────┴──────────┘");
                }
                else
                {
                    System.Console.WriteLine("📭 Nessun mezzo disponibile al momento");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }
        }

        static async Task ListParcheggi()
        {
            System.Console.Clear();
            await MostraParcheggi();
            await WaitKey();
        }

        static async Task MostraParcheggi()
        {
            System.Console.WriteLine("🅿️ === PARCHEGGI === ");
            System.Console.WriteLine();

            try
            {
                var response = await httpClient.GetAsync($"{API_BASE}/parcheggi");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Console.WriteLine($"Errore nel recuperare i parcheggi: {json}");
                    return;
                }

                var parcheggi = JsonSerializer.Deserialize<ParcheggioDto[]>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (parcheggi?.Length > 0)
                {
                    foreach (var parcheggio in parcheggi)
                    {
                        var disponibilita = $"{parcheggio.PostiLiberi}/{parcheggio.Capienza}";
                        var status = parcheggio.PostiLiberi > 0 ? "🟢" : "🔴";
                        
                        System.Console.WriteLine($"{status} {parcheggio.Id}. {parcheggio.Nome}");
                        System.Console.WriteLine($"   📍 {parcheggio.Indirizzo}");
                        System.Console.WriteLine($"   📊 Posti liberi: {disponibilita}");
                        System.Console.WriteLine($"   Mezzi presenti: {parcheggio.Mezzi.Count}");
                        System.Console.WriteLine();
                    }
                }
                else
                {
                    System.Console.WriteLine("📭 Nessun parcheggio trovato");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }
        }

        static async Task IniziaCorsa()
        {
            System.Console.Clear();
            System.Console.WriteLine("🚀 === INIZIA CORSA === ");
            System.Console.WriteLine();

            try
            {
                // Mostra mezzi disponibili
                await MostraMezziDisponibili();
                
                System.Console.Write("Inserisci ID del mezzo: ");
                if (!int.TryParse(System.Console.ReadLine(), out var mezzoId))
                {
                    System.Console.WriteLine("ID mezzo non valido");
                    await WaitKey();
                    return;
                }

                var comando = new IniziaCorsa { UtenteId = currentUser!.Id, MezzoId = mezzoId };
                var json = JsonSerializer.Serialize(comando);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{API_BASE}/corse/inizia", content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var corsa = JsonSerializer.Deserialize<CorsaDto>(result, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    System.Console.WriteLine("Corsa iniziata con successo!");
                    System.Console.WriteLine($"   🆔 ID Corsa: {corsa?.Id}");
                    System.Console.WriteLine($"   ⏰ Inizio: {corsa?.Inizio:dd/MM/yyyy HH:mm:ss}");
                    System.Console.WriteLine($"   Mezzo: {mezzoId}");
                    System.Console.WriteLine();
                    System.Console.WriteLine("🎯 Buon viaggio! Ricorda di terminare la corsa quando arrivi a destinazione.");
                }
                else
                {
                    System.Console.WriteLine($"Errore nell'iniziare la corsa: {result}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }

            await WaitKey();
        }

        static async Task TerminaCorsa()
        {
            System.Console.Clear();
            System.Console.WriteLine("🏁 === TERMINA CORSA === ");
            System.Console.WriteLine();

            try
            {
                // Mostra corse attive dell'utente
                await MostraCorsaAttiva();

                System.Console.Write("🆔 Inserisci ID della corsa da terminare: ");
                if (!int.TryParse(System.Console.ReadLine(), out var corsaId))
                {
                    System.Console.WriteLine("ID corsa non valido");
                    await WaitKey();
                    return;
                }

                // Mostra parcheggi disponibili
                await MostraParcheggi();

                System.Console.Write("🅿️ Inserisci ID del parcheggio di destinazione: ");
                if (!int.TryParse(System.Console.ReadLine(), out var parcheggioId))
                {
                    System.Console.WriteLine("ID parcheggio non valido");
                    await WaitKey();
                    return;
                }

                // Chiedi se vuole segnalare problemi di manutenzione
                System.Console.WriteLine();
                System.Console.WriteLine("🔧 === SEGNALAZIONE MANUTENZIONE === ");
                System.Console.Write("Vuoi segnalare problemi di manutenzione per questo mezzo? (s/n): ");
                var segnalaInput = System.Console.ReadLine()?.ToLower();
                var segnalaManutenzione = segnalaInput == "s" || segnalaInput == "si" || segnalaInput == "y" || segnalaInput == "yes";

                string? descrizioneManutenzione = null;
                if (segnalaManutenzione)
                {
                    System.Console.WriteLine("📝 Descrivi il problema riscontrato:");
                    descrizioneManutenzione = System.Console.ReadLine();
                    
                    if (string.IsNullOrEmpty(descrizioneManutenzione))
                    {
                        System.Console.WriteLine("La descrizione è obbligatoria per la segnalazione");
                        await WaitKey();
                        return;
                    }
                }

                var terminaCorsa = new TerminaCorsa 
                { 
                    ParcheggioDestinazioneId = parcheggioId,
                    SegnalaManutenzione = segnalaManutenzione,
                    DescrizioneManutenzione = descrizioneManutenzione
                };

                var json = JsonSerializer.Serialize(terminaCorsa);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PutAsync($"{API_BASE}/corse/{corsaId}/termina", content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var corsa = JsonSerializer.Deserialize<CorsaDto>(result, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    System.Console.WriteLine("Corsa terminata con successo!");
                    System.Console.WriteLine($"   ⏱️  Durata: {corsa?.DurataMinuti} minuti");
                    System.Console.WriteLine($"   💰 Costo totale: €{corsa?.CostoTotale:F2}");
                    
                    if (segnalaManutenzione)
                    {
                        System.Console.WriteLine("   🔧 Segnalazione manutenzione inviata!");
                        System.Console.WriteLine("   ⚠️  Il mezzo è stato messo in manutenzione");
                    }

                    // Aggiorna il credito dell'utente
                    await AggiornaCreditoUtente();
                }
                else
                {
                    System.Console.WriteLine($"Errore nel terminare la corsa: {result}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }

            await WaitKey();
        }

        static async Task MostraCorsaAttiva()
        {
            try
            {
                var response = await httpClient.GetAsync($"{API_BASE}/corse/utente/{currentUser!.Id}");
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var corse = JsonSerializer.Deserialize<CorsaDto[]>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    var corsaAttiva = corse?.FirstOrDefault(c => c.Stato == "InCorso");
                    if (corsaAttiva != null)
                    {
                        System.Console.WriteLine("🔄 Corsa attiva:");
                        System.Console.WriteLine($"   🆔 ID: {corsaAttiva.Id}");
                        System.Console.WriteLine($"   Mezzo: {corsaAttiva.MezzoId}");
                        System.Console.WriteLine($"   ⏰ Inizio: {corsaAttiva.Inizio:dd/MM/yyyy HH:mm:ss}");
                        System.Console.WriteLine();
                    }
                    else
                    {
                        System.Console.WriteLine("ℹ️  Non hai corse attive");
                        System.Console.WriteLine();
                    }
                }
            }
            catch
            {
                // Ignora errori nella visualizzazione delle corse attive
            }
        }

        static async Task StoricoCorse()
        {
            System.Console.Clear();
            System.Console.WriteLine("📜 === STORICO CORSE === ");
            System.Console.WriteLine();

            try
            {
                var response = await httpClient.GetAsync($"{API_BASE}/corse/utente/{currentUser!.Id}");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Console.WriteLine($"Errore nel recuperare lo storico: {json}");
                    await WaitKey();
                    return;
                }

                var corse = JsonSerializer.Deserialize<CorsaDto[]>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (corse?.Length > 0)
                {
                    System.Console.WriteLine("┌──────┬──────────────────────┬────────┬──────────────┬──────────┐");
                    System.Console.WriteLine("│ ID   │ Data e Ora           │ Durata │ Costo        │ Stato    │");
                    System.Console.WriteLine("├──────┼──────────────────────┼────────┼──────────────┼──────────┤");

                    foreach (var corsa in corse.OrderByDescending(c => c.Inizio))
                    {
                        var durata = corsa.Fine.HasValue ? $"{corsa.DurataMinuti} min" : "In corso";
                        var costo = corsa.CostoTotale > 0 ? $"€{corsa.CostoTotale:F2}" : "N/A";
                        var dataOra = corsa.Inizio.ToString("dd/MM/yy HH:mm");

                        System.Console.WriteLine($"│ {corsa.Id,-4} │ {dataOra,-20} │ {durata,-6} │ {costo,-12} │ {corsa.Stato,-8} │");
                    }
                    System.Console.WriteLine("└──────┴──────────────────────┴────────┴──────────────┴──────────┘");
                }
                else
                {
                    System.Console.WriteLine("📭 Non hai ancora effettuato corse");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }

            await WaitKey();
        }

        static async Task RicaricaCredito()
        {
            System.Console.Clear();
            System.Console.WriteLine("💳 === RICARICA CREDITO === ");
            System.Console.WriteLine();
            System.Console.WriteLine($"💰 Credito attuale: €{currentUser?.Credito:F2}");
            System.Console.WriteLine();

            System.Console.Write("💵 Inserisci l'importo da ricaricare (€5-€500): ");
            if (!decimal.TryParse(System.Console.ReadLine(), out var importo) || importo < 5 || importo > 500)
            {
                System.Console.WriteLine("Importo non valido. Deve essere tra €5 e €500");
                await WaitKey();
                return;
            }

            try
            {
                var ricaricaRequest = new RicaricaCreditoDto 
                { 
                    UtenteId = currentUser!.Id,
                    Importo = importo,
                    MetodoPagamento = "CartaCredito"
                };
                var json = JsonSerializer.Serialize(ricaricaRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{API_BASE}/user/ricarica-credito", content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var ricaricaResponse = JsonSerializer.Deserialize<RicaricaResponseDto>(result, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    System.Console.WriteLine($"Ricarica effettuata con successo!");
                    System.Console.WriteLine($"   💰 Importo ricaricato: €{importo:F2}");
                    System.Console.WriteLine($"   🆔 Transaction ID: {ricaricaResponse?.TransactionId}");
                    
                    // Aggiorna il credito dell'utente
                    await AggiornaCreditoUtente();
                    
                    System.Console.WriteLine($"   💰 Nuovo credito: €{currentUser?.Credito:F2}");
                }
                else
                {
                    var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
                    var errorMessage = errorResponse.TryGetProperty("message", out var messageProp) 
                        ? messageProp.GetString() 
                        : "Errore sconosciuto";
                    System.Console.WriteLine($"Errore nella ricarica: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }

            await WaitKey();
        }

        static async Task ProfiloUtente()
        {
            System.Console.Clear();
            System.Console.WriteLine("👤 === PROFILO UTENTE === ");
            System.Console.WriteLine();

            if (currentUser != null)
            {
                System.Console.WriteLine($"🆔 ID: {currentUser.Id}");
                System.Console.WriteLine($"👤 Nome: {currentUser.Nome}");
                System.Console.WriteLine($"📧 Email: {currentUser.Email}");
                System.Console.WriteLine($"💰 Credito: €{currentUser.Credito:F2}");
                System.Console.WriteLine($"📅 Data registrazione: {currentUser.DataRegistrazione:dd/MM/yyyy}");
            }
            else
            {
                System.Console.WriteLine("Informazioni utente non disponibili");
            }

            await WaitKey();
        }

        static async Task TestMqttCommands()
        {
            System.Console.Clear();
            System.Console.WriteLine("📡 === TEST COMANDI MQTT === ");
            System.Console.WriteLine();
            System.Console.WriteLine("1. 🔓 Sblocca mezzo");
            System.Console.WriteLine("2. 🔒 Blocca mezzo");
            System.Console.WriteLine("3. 💡 Controllo LED slot");
            System.Console.WriteLine("4. 📊 Status MQTT");
            System.Console.WriteLine("0. 🔙 Torna al menu");
            System.Console.WriteLine();
            System.Console.Write("Scegli un'opzione: ");

            var choice = System.Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        System.Console.Write("ID Mezzo: ");
                        if (int.TryParse(System.Console.ReadLine(), out var mezzoId))
                        {
                            var response = await httpClient.PostAsync($"{API_BASE}/mqtt/unlock/{mezzoId}", null);
                            var result = await response.Content.ReadAsStringAsync();
                            System.Console.WriteLine(response.IsSuccessStatusCode ? "Comando inviato" : $"Errore: {result}");
                        }
                        break;

                    case "2":
                        System.Console.Write("ID Mezzo: ");
                        if (int.TryParse(System.Console.ReadLine(), out var mezzoId2))
                        {
                            var response = await httpClient.PostAsync($"{API_BASE}/mqtt/lock/{mezzoId2}", null);
                            var result = await response.Content.ReadAsStringAsync();
                            System.Console.WriteLine(response.IsSuccessStatusCode ? "Comando inviato" : $"Errore: {result}");
                        }
                        break;

                    case "3":
                        System.Console.Write("🅿️ ID Slot: ");
                        if (int.TryParse(System.Console.ReadLine(), out var slotId))
                        {
                            System.Console.Write("🎨 Colore (green/red/yellow/blue): ");
                            var color = System.Console.ReadLine() ?? "green";

                            var ledCommand = new { Color = color, Pattern = "solid" };
                            var json = JsonSerializer.Serialize(ledCommand);
                            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                            var response = await httpClient.PostAsync($"{API_BASE}/mqtt/led/{slotId}", content);
                            var result = await response.Content.ReadAsStringAsync();
                            System.Console.WriteLine(response.IsSuccessStatusCode ? "Comando inviato" : $"Errore: {result}");
                        }
                        break;

                    case "4":
                        var statusResponse = await httpClient.GetAsync($"{API_BASE}/mqtt/status");
                        var statusResult = await statusResponse.Content.ReadAsStringAsync();
                        System.Console.WriteLine($"📡 Status MQTT: {statusResult}");
                        break;

                    case "0":
                        return;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore: {ex.Message}");
            }

            await WaitKey();
        }

        static async Task StatusSistema()
        {
            System.Console.Clear();
            System.Console.WriteLine("📊 === STATUS SISTEMA === ");
            System.Console.WriteLine();

            try
            {
                // Test API
                var apiResponse = await httpClient.GetAsync($"{API_BASE}/mezzi");
                System.Console.WriteLine($"🌐 API Status: {(apiResponse.IsSuccessStatusCode ? "Connesso" : "Disconnesso")}");

                // Test MQTT
                try
                {
                    var mqttResponse = await httpClient.GetAsync($"{API_BASE}/mqtt/status");
                    if (mqttResponse.IsSuccessStatusCode)
                    {
                        var mqttStatus = await mqttResponse.Content.ReadAsStringAsync();
                        System.Console.WriteLine($"📡 MQTT Status: {mqttStatus}");
                    }
                    else
                    {
                        System.Console.WriteLine("📡 MQTT Status: Non disponibile");
                    }
                }
                catch
                {
                    System.Console.WriteLine("📡 MQTT Status: Errore di connessione");
                }

                // Statistiche generali
                System.Console.WriteLine();
                System.Console.WriteLine("📈 === STATISTICHE === ");

                var mezziResponse = await httpClient.GetAsync($"{API_BASE}/mezzi");
                if (mezziResponse.IsSuccessStatusCode)
                {
                    var mezziJson = await mezziResponse.Content.ReadAsStringAsync();
                    var mezzi = JsonSerializer.Deserialize<MezzoDto[]>(mezziJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    System.Console.WriteLine($"Mezzi totali: {mezzi?.Length ?? 0}");
                }

                var parcheggiwResponse = await httpClient.GetAsync($"{API_BASE}/parcheggi");
                if (parcheggiwResponse.IsSuccessStatusCode)
                {
                    var parcheggwJson = await parcheggiwResponse.Content.ReadAsStringAsync();
                    var parcheggi = JsonSerializer.Deserialize<ParcheggioDto[]>(parcheggwJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    System.Console.WriteLine($"🅿️ Parcheggi totali: {parcheggi?.Length ?? 0}");
                }

                System.Console.WriteLine($"⏰ Timestamp: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Errore nel controllo status: {ex.Message}");
            }

            await WaitKey();
        }

        // === HELPER METHODS ===
        static async Task WaitKey()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("Premi un tasto per continuare...");
            System.Console.ReadKey();
        }

        static async Task AggiornaCreditoUtente()
        {
            try
            {
                var response = await httpClient.GetAsync($"{API_BASE}/user/profile");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var utente = JsonSerializer.Deserialize<UtenteDto>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    if (utente != null)
                    {
                        currentUser = utente;
                    }
                }
            }
            catch
            {
                // Ignora errori nell'aggiornamento del credito
            }
        }
    }

    // === DTO CLASSES ===
    public class LoginDto
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AuthResultDto
    {
        public bool Success { get; set; }
        public string Token { get; set; } = "";
        public string Message { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public UtenteDto? User { get; set; }
    }

    public class TerminaCorsa
    {
        public int ParcheggioDestinazioneId { get; set; }
        public bool SegnalaManutenzione { get; set; } = false;
        public string? DescrizioneManutenzione { get; set; }
    }

    public class RicaricaCreditoDto
    {
        public int UtenteId { get; set; }
        public decimal Importo { get; set; }
        public string MetodoPagamento { get; set; } = "CartaCredito";
    }

    public class RicaricaResponseDto
    {
        public bool Success { get; set; }
        public decimal NuovoCredito { get; set; }
        public string? Message { get; set; }
        public string? TransactionId { get; set; }
    }
}
