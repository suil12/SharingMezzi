using SharingMezzi.Core.DTOs;
using SharingMezzi.Core.Entities;
using SharingMezzi.Core.Interfaces.Repositories;
using SharingMezzi.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace SharingMezzi.Core.Services
{
    /// <summary>
    /// Interfaccia per il broker MQTT - permette l'invio di comandi agli attuatori
    /// </summary>
    public interface IMqttActuatorService
    {
        Task SendUnlockCommand(int mezzoId, int? corsaId = null);
        Task SendLockCommand(int mezzoId, int? corsaId = null);
    }    public class CorsaService : ICorsaService
    {
        private readonly IRepository<Corsa> _corsaRepository;
        private readonly IMezzoRepository _mezzoRepository;
        private readonly IRepository<Utente> _utenteRepository;
        private readonly IRepository<Parcheggio> _parcheggioRepository;
        private readonly IRepository<SegnalazioneManutenzione> _segnalazioneRepository;
        private readonly ILogger<CorsaService> _logger;
        private readonly IMqttActuatorService? _mqttActuatorService;

        public CorsaService(
            IRepository<Corsa> corsaRepository,
            IMezzoRepository mezzoRepository,
            IRepository<Utente> utenteRepository,
            IRepository<Parcheggio> parcheggioRepository,
            IRepository<SegnalazioneManutenzione> segnalazioneRepository,
            ILogger<CorsaService> logger,
            IMqttActuatorService? mqttActuatorService = null)        {
            _corsaRepository = corsaRepository;
            _mezzoRepository = mezzoRepository;
            _utenteRepository = utenteRepository;
            _parcheggioRepository = parcheggioRepository;
            _segnalazioneRepository = segnalazioneRepository;
            _logger = logger;
            _mqttActuatorService = mqttActuatorService;
        }

        public async Task<CorsaDto> IniziaCorsa(IniziaCorsa comando)
        {
            // Verifica mezzo disponibile
            var mezzo = await _mezzoRepository.GetByIdAsync(comando.MezzoId);
            if (mezzo == null || mezzo.Stato != StatoMezzo.Disponibile)
                throw new InvalidOperationException("Mezzo non disponibile");

            // NUOVO: Verifica utente e credito
            var utente = await _utenteRepository.GetByIdAsync(comando.UtenteId);
            if (utente == null)
                throw new InvalidOperationException("Utente non trovato");
                
            if (utente.Stato != StatoUtente.Attivo)
                throw new InvalidOperationException($"Utente {utente.Stato}. Contattare l'assistenza.");
              // Verifica credito minimo (tariffa fissa + costo prima mezz'ora)
            decimal costoMinimo = mezzo.TariffaFissa + CalculateCostoMinimo(mezzo.TariffaPerMinuto);
            if (!utente.HaCreditoSufficiente(costoMinimo))
            {
                throw new InvalidOperationException(
                    $"Credito insufficiente. Richiesti almeno €{costoMinimo:F2} " +
                    $"(tariffa fissa €{mezzo.TariffaFissa:F2} + costo minimo prima mezz'ora €{CalculateCostoMinimo(mezzo.TariffaPerMinuto):F2}), " +
                    $"disponibili €{utente.Credito:F2}");
            }

            // Verifica se utente ha già una corsa attiva
            var corseUtente = await _corsaRepository.GetAllAsync();
            var corsaAttiva = corseUtente.FirstOrDefault(c => 
                c.UtenteId == comando.UtenteId && c.Stato == StatoCorsa.InCorso);
            
            if (corsaAttiva != null)
                throw new InvalidOperationException("Hai già una corsa in corso");            // Crea corsa
            var corsa = new Corsa
            {
                UtenteId = comando.UtenteId,
                MezzoId = comando.MezzoId,
                ParcheggioPartenzaId = mezzo.ParcheggioId ?? 0,
                Inizio = DateTime.UtcNow,
                Stato = StatoCorsa.InCorso,
                DurataMinuti = 0, // Inizializza a 0 per una corsa appena iniziata
                CostoTotale = 0   // Inizializza a 0 per una corsa appena iniziata
            };

            corsa = await _corsaRepository.AddAsync(corsa);

            await _mezzoRepository.UpdateStatusAsync(comando.MezzoId, StatoMezzo.Occupato);

            try
            {
                if (_mqttActuatorService != null)
                {
                    await _mqttActuatorService.SendUnlockCommand(comando.MezzoId, corsa.Id);
                    _logger.LogInformation("Sent unlock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                        comando.MezzoId, corsa.Id);
                }
                else
                {
                    _logger.LogWarning("MQTT Actuator Service not available - cannot send unlock command for Mezzo {MezzoId}", 
                        comando.MezzoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send unlock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                    comando.MezzoId, corsa.Id);
            }            _logger.LogInformation("Started ride {CorsaId} for user {UserId} with vehicle {MezzoId}", 
                corsa.Id, comando.UtenteId, comando.MezzoId);

            return await MapToDtoWithDetails(corsa);
        }        public async Task<CorsaDto> TerminaCorsa(int corsaId, TerminaCorsa comando)
        {
            var corsa = await _corsaRepository.GetByIdAsync(corsaId);
            if (corsa == null || corsa.Stato != StatoCorsa.InCorso)
                throw new InvalidOperationException("Corsa non trovata o già terminata");

            var utente = await _utenteRepository.GetByIdAsync(corsa.UtenteId);
            if (utente == null)
                throw new InvalidOperationException("Utente non trovato");

            var mezzo = await _mezzoRepository.GetByIdAsync(corsa.MezzoId);
            if (mezzo == null)
                throw new InvalidOperationException("Mezzo non trovato");

            // VALIDAZIONE: Verifica che il parcheggio di destinazione esista
            var parcheggioDestinazione = await _parcheggioRepository.GetByIdAsync(comando.ParcheggioDestinazioneId);
            if (parcheggioDestinazione == null)
                throw new InvalidOperationException($"Parcheggio di destinazione con ID {comando.ParcheggioDestinazioneId} non trovato");

            // Calcola durata e costo
            corsa.Fine = DateTime.UtcNow;
            corsa.DurataMinuti = (int)(corsa.Fine.Value - corsa.Inizio).TotalMinutes;
            corsa.ParcheggioDestinazioneId = comando.ParcheggioDestinazioneId;
            corsa.CostoTotale = await CalcolaCosto(corsaId);// NUOVO: Addebita credito
            utente.AddebitaCredito(corsa.CostoTotale);
            

            if (mezzo.Tipo == TipoMezzo.BiciMuscolare)
            {
                int puntiEco = CalcolaPuntiEco(corsa.DurataMinuti);
                utente.AggiungiPuntiEco(puntiEco);
                _logger.LogInformation("Assegnati {Punti} punti eco a utente {UserId}", 
                    puntiEco, utente.Id);
            }

            // Aggiorna entità
            await _utenteRepository.UpdateAsync(utente);
              corsa.Stato = utente.Credito >= 0 ? StatoCorsa.Completata : StatoCorsa.CompletataConDebito;
            await _corsaRepository.UpdateAsync(corsa);
            
            // NUOVO: Gestione segnalazione manutenzione
            StatoMezzo nuovoStatoMezzo = StatoMezzo.Disponibile;
            
            if (comando.SegnalaManutenzione)
            {
                await CreaSegnalazioneManutenzione(corsa, comando.DescrizioneManutenzione);
                nuovoStatoMezzo = StatoMezzo.Manutenzione;
                _logger.LogWarning("Mezzo {MezzoId} segnalato per manutenzione dall'utente {UserId} durante corsa {CorsaId}", 
                    corsa.MezzoId, corsa.UtenteId, corsaId);
            }
            
            await _mezzoRepository.UpdateStatusAsync(corsa.MezzoId, nuovoStatoMezzo);

            try
            {
                if (_mqttActuatorService != null)
                {
                    await _mqttActuatorService.SendLockCommand(corsa.MezzoId, corsaId);
                    _logger.LogInformation("Sent lock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                        corsa.MezzoId, corsaId);
                }
                else
                {
                    _logger.LogWarning("MQTT Actuator Service not available - cannot send lock command for Mezzo {MezzoId}", 
                        corsa.MezzoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send lock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                    corsa.MezzoId, corsaId);
            }            _logger.LogInformation("Ended ride {CorsaId}, duration: {Duration} min, cost: €{Cost}, maintenance: {Maintenance}", 
                corsaId, corsa.DurataMinuti, corsa.CostoTotale, comando.SegnalaManutenzione);

            return await MapToDtoWithDetails(corsa);
        }        public async Task<decimal> CalcolaCosto(int corsaId)
        {
            var corsa = await _corsaRepository.GetByIdAsync(corsaId);
            if (corsa == null) return 0;

            var mezzo = await _mezzoRepository.GetByIdAsync(corsa.MezzoId);
            if (mezzo == null) return 0;

            // Calcolo con tariffa fissa + tariffa per minuti
            decimal costoFisso = mezzo.TariffaFissa; // Tariffa fissa di attivazione (€1.00)
            decimal costoVariabile = 0;
            int minuti = corsa.DurataMinuti;
            
            if (minuti <= 30)
            {
                // Prima mezz'ora: costo fisso ridotto per minuti
                costoVariabile = CalculateCostoMinimo(mezzo.TariffaPerMinuto);
            }
            else
            {
                // Oltre mezz'ora: costo minimo prima mezz'ora + tariffa normale per minuti extra
                costoVariabile = CalculateCostoMinimo(mezzo.TariffaPerMinuto) + 
                                (minuti - 30) * mezzo.TariffaPerMinuto;
            }

            return costoFisso + costoVariabile;
        }

        private decimal CalculateCostoMinimo(decimal tariffaPerMinuto)
        {
            // Costo fisso prima mezz'ora = 50% della tariffa normale per 30 minuti
            return (tariffaPerMinuto * 30) * 0.5m;
        }

        private int CalcolaPuntiEco(int durataMinuti)
        {
            // 1 punto eco per ogni minuto di utilizzo bici muscolare
            // Bonus: +10 punti ogni 30 minuti
            int punti = durataMinuti;
            int bonus = (durataMinuti / 30) * 10;
            return punti + bonus;
        }        public async Task<IEnumerable<CorsaDto>> GetCorseUtente(int utenteId)
        {
            var corse = await _corsaRepository.GetAllAsync();
            var corseUtente = corse.Where(c => c.UtenteId == utenteId).ToList();
            
            var result = new List<CorsaDto>();
            foreach (var corsa in corseUtente)
            {
                result.Add(await MapToDtoWithDetails(corsa));
            }
            
            return result;
        }

        public async Task<CorsaDto?> GetCorsaAttiva(int utenteId)
        {
            var corse = await _corsaRepository.GetAllAsync();
            var corsaAttiva = corse.FirstOrDefault(c => c.UtenteId == utenteId && c.Stato == StatoCorsa.InCorso);
            return corsaAttiva != null ? await MapToDtoWithDetails(corsaAttiva) : null;
        }private CorsaDto MapToDto(Corsa corsa)
        {
            return new CorsaDto
            {
                Id = corsa.Id,
                UtenteId = corsa.UtenteId,
                MezzoId = corsa.MezzoId,                ParcheggioPartenzaId = corsa.ParcheggioPartenzaId,
                ParcheggioDestinazioneId = corsa.ParcheggioDestinazioneId,
                Inizio = corsa.Inizio,              
                Fine = corsa.Fine,               
                DurataMinuti = corsa.DurataMinuti, 
                CostoTotale = corsa.CostoTotale,                Stato = corsa.Stato.ToString(),
                
                // Informazioni del mezzo (se disponibili tramite navigation property)
                MezzoModello = corsa.Mezzo?.Modello ?? "N/A",
                MezzoTipo = corsa.Mezzo?.Tipo.ToString() ?? "N/A",
                TariffaPerMinuto = corsa.Mezzo?.TariffaPerMinuto ?? 0,
                TariffaFissa = corsa.Mezzo?.TariffaFissa ?? 1.00m,
                IsElettrico = corsa.Mezzo?.IsElettrico ?? false,
                
                // Informazioni parcheggi
                NomeParcheggioInizio = corsa.ParcheggioPartenza?.Nome ?? "N/A",
                NomeParcheggioFine = corsa.ParcheggioDestinazione?.Nome
            };
        }

        private async Task<CorsaDto> MapToDtoWithDetails(Corsa corsa)
        {
            // Carica le informazioni del mezzo se non sono presenti
            var mezzo = corsa.Mezzo ?? await _mezzoRepository.GetByIdAsync(corsa.MezzoId);
            
            return new CorsaDto
            {
                Id = corsa.Id,
                UtenteId = corsa.UtenteId,
                MezzoId = corsa.MezzoId,                ParcheggioPartenzaId = corsa.ParcheggioPartenzaId,
                ParcheggioDestinazioneId = corsa.ParcheggioDestinazioneId,
                Inizio = corsa.Inizio,              
                Fine = corsa.Fine,               
                DurataMinuti = corsa.DurataMinuti, 
                CostoTotale = corsa.CostoTotale,                Stato = corsa.Stato.ToString(),
                
                // Informazioni del mezzo caricate dal database
                MezzoModello = mezzo?.Modello ?? "N/A",
                MezzoTipo = mezzo?.Tipo.ToString() ?? "N/A", 
                TariffaPerMinuto = mezzo?.TariffaPerMinuto ?? 0,
                TariffaFissa = mezzo?.TariffaFissa ?? 1.00m,
                IsElettrico = mezzo?.IsElettrico ?? false,
                  // Informazioni parcheggi (se disponibili)
                NomeParcheggioInizio = corsa.ParcheggioPartenza?.Nome ?? "N/A",
                NomeParcheggioFine = corsa.ParcheggioDestinazione?.Nome
            };
        }

        /// <summary>
        /// Crea una segnalazione di manutenzione per il mezzo
        /// </summary>
        private async Task CreaSegnalazioneManutenzione(Corsa corsa, string? descrizione)
        {
            var segnalazione = new SegnalazioneManutenzione
            {
                MezzoId = corsa.MezzoId,
                UtenteId = corsa.UtenteId,
                CorsaId = corsa.Id,
                Descrizione = descrizione ?? "Segnalazione generica durante la corsa",
                Stato = StatoSegnalazione.Aperta,
                Priorita = PrioritaSegnalazione.Media,
                DataSegnalazione = DateTime.UtcNow
            };

            await _segnalazioneRepository.AddAsync(segnalazione);
            
            _logger.LogInformation("Created maintenance report {SegnalazioneId} for Mezzo {MezzoId} by User {UserId}", 
                segnalazione.Id, corsa.MezzoId, corsa.UtenteId);
        }
    }
}