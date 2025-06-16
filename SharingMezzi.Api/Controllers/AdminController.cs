using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharingMezzi.Core.DTOs;
using SharingMezzi.Core.Interfaces.Services;
using SharingMezzi.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharingMezzi.Core.Entities;
using SharingMezzi.IoT.Services;

namespace SharingMezzi.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Solo utenti autenticati
    public class AdminController : ControllerBase
    {
        private readonly SharingMezziContext _context;
        private readonly IMezzoService _mezzoService;
        private readonly IParcheggioService _parcheggioService;
        private readonly ConnectedIoTClientsService _iotClientsService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            SharingMezziContext context,
            IMezzoService mezzoService,
            IParcheggioService parcheggioService,
            ConnectedIoTClientsService iotClientsService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _mezzoService = mezzoService;
            _parcheggioService = parcheggioService;
            _iotClientsService = iotClientsService;
            _logger = logger;
        }

        /// <summary>
        /// Ottieni statistiche dashboard admin
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<AdminStatisticsDto>> GetStatistics()
        {
            try
            {
                var mezzi = await _context.Mezzi.ToListAsync();
                var parcheggi = await _context.Parcheggi.ToListAsync();
                var corseAttive = await _context.Corse
                    .Where(c => c.Stato == StatoCorsa.InCorso)
                    .CountAsync();

                var stats = new AdminStatisticsDto
                {
                    TotalMezzi = mezzi.Count,
                    MezziDisponibili = mezzi.Count(m => m.Stato == StatoMezzo.Disponibile),
                    MezziInUso = mezzi.Count(m => m.Stato == StatoMezzo.Occupato),
                    MezziManutenzione = mezzi.Count(m => m.Stato == StatoMezzo.Manutenzione),
                    TotalParcheggi = parcheggi.Count,
                    BatteriaBassa = mezzi.Count(m => m.LivelloBatteria < 20),
                    CorseAttive = corseAttive,
                    UltimoAggiornamento = DateTime.UtcNow
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero delle statistiche admin");
                return StatusCode(500, "Errore interno del server");
            }
        }

        /// <summary>
        /// Ottieni mezzi che necessitano manutenzione
        /// </summary>
        [HttpGet("maintenance-alerts")]
        public async Task<ActionResult<IEnumerable<MaintenanceAlertDto>>> GetMaintenanceAlerts()
        {
            try
            {
                var alerts = new List<MaintenanceAlertDto>();

                // Mezzi con batteria bassa
                var lowBatteryMezzi = await _context.Mezzi
                    .Where(m => m.LivelloBatteria < 20)
                    .ToListAsync();

                foreach (var mezzo in lowBatteryMezzi)
                {
                    alerts.Add(new MaintenanceAlertDto
                    {
                        MezzoId = mezzo.Id,
                        Type = "battery",
                        Priority = mezzo.LivelloBatteria < 10 ? "critical" : "high",
                        Message = $"Batteria al {mezzo.LivelloBatteria}%",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Mezzi in manutenzione
                var maintenanceMezzi = await _context.Mezzi
                    .Where(m => m.Stato == StatoMezzo.Manutenzione)
                    .ToListAsync();

                foreach (var mezzo in maintenanceMezzi)
                {
                    alerts.Add(new MaintenanceAlertDto
                    {
                        MezzoId = mezzo.Id,
                        Type = "maintenance",
                        Priority = "medium",
                        Message = "Mezzo in manutenzione",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return Ok(alerts.OrderByDescending(a => a.Priority == "critical" ? 3 : 
                                                      a.Priority == "high" ? 2 : 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero degli alert di manutenzione");
                return StatusCode(500, "Errore interno del server");
            }
        }

        /// <summary>
        /// Forza aggiornamento di tutti i parcheggi
        /// </summary>
        [HttpPost("refresh-all")]
        public async Task<IActionResult> RefreshAllParkings()
        {
            try
            {
                var parcheggi = await _context.Parcheggi.ToListAsync();
                
                foreach (var parcheggio in parcheggi)
                {
                    await _parcheggioService.UpdatePostiLiberiAsync(parcheggio.Id);
                }

                _logger.LogInformation("Aggiornamento forzato di tutti i parcheggi completato");
                return Ok(new { message = "Tutti i parcheggi sono stati aggiornati" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'aggiornamento forzato dei parcheggi");
                return StatusCode(500, "Errore interno del server");
            }
        }

        /// <summary>
        /// Ottieni stato sistema IoT/MQTT
        /// </summary>
        [HttpGet("system-status")]
        public ActionResult<SystemStatusDto> GetSystemStatus()
        {
            try
            {
                // Ottieni statistiche reali dei dispositivi IoT
                var iotStats = _iotClientsService.GetConnectionStats();
                dynamic stats = iotStats;
                var connectedDevices = (int)stats.ConnectedClients;
                
                var status = new SystemStatusDto
                {
                    MqttBrokerStatus = "online", // Controlla il tuo SharingMezziBroker
                    IoTDevicesConnected = connectedDevices, // Ora usa i dati reali!
                    SignalRStatus = "active",     // Controlla SignalR hubs
                    DatabaseStatus = "healthy",   // Controlla connessione DB
                    LastUpdate = DateTime.UtcNow
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero dello stato sistema");
                return StatusCode(500, "Errore interno del server");
            }
        }

        /// <summary>
        /// Programma manutenzione per un mezzo
        /// </summary>
        [HttpPost("schedule-maintenance/{mezzoId}")]
        public async Task<IActionResult> ScheduleMaintenance(int mezzoId, [FromBody] ScheduleMaintenanceDto request)
        {
            try
            {
                var mezzo = await _context.Mezzi.FindAsync(mezzoId);
                if (mezzo == null)
                {
                    return NotFound($"Mezzo {mezzoId} non trovato");
                }

                mezzo.Stato = StatoMezzo.Manutenzione;
                mezzo.UltimaManutenzione = DateTime.UtcNow;

                // Crea record di manutenzione (se hai una tabella per questo)
                var segnalazione = new SegnalazioneManutenzione
                {
                    MezzoId = mezzoId,
                    Descrizione = request.Note ?? "Manutenzione programmata da admin",
                    DataSegnalazione = DateTime.UtcNow,
                    Stato = StatoSegnalazione.Aperta
                };

                _context.SegnalazioniManutenzione.Add(segnalazione);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Manutenzione programmata per mezzo {MezzoId}", mezzoId);
                
                return Ok(new { message = $"Manutenzione programmata per mezzo {mezzoId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella programmazione manutenzione per mezzo {MezzoId}", mezzoId);
                return StatusCode(500, "Errore interno del server");
            }
        }
    }

    // DTO specifici per Admin
    public class AdminStatisticsDto
    {
        public int TotalMezzi { get; set; }
        public int MezziDisponibili { get; set; }
        public int MezziInUso { get; set; }
        public int MezziManutenzione { get; set; }
        public int TotalParcheggi { get; set; }
        public int BatteriaBassa { get; set; }
        public int CorseAttive { get; set; }
        public DateTime UltimoAggiornamento { get; set; }
    }

    public class MaintenanceAlertDto
    {
        public int MezzoId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class SystemStatusDto
    {
        public string MqttBrokerStatus { get; set; } = string.Empty;
        public int IoTDevicesConnected { get; set; }
        public string SignalRStatus { get; set; } = string.Empty;
        public string DatabaseStatus { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
    }

    public class ScheduleMaintenanceDto
    {
        public string? Note { get; set; }
        public DateTime? ScheduledDate { get; set; }
    }
}