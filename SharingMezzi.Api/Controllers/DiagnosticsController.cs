using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SharingMezzi.Core.Services;
using SharingMezzi.IoT.Services;
using Microsoft.Extensions.Logging;

namespace SharingMezzi.Api.Controllers
{
    /// <summary>
    /// Controller per diagnostica e testing del sistema MQTT
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ConnectedIoTClientsService _iotClientsService;
        private readonly IMqttActuatorService _mqttActuatorService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            ConnectedIoTClientsService iotClientsService,
            IMqttActuatorService mqttActuatorService,
            ILogger<DiagnosticsController> logger)
        {
            _iotClientsService = iotClientsService;
            _mqttActuatorService = mqttActuatorService;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene statistiche di tutti i client IoT connessi
        /// </summary>
        [HttpGet("iot-status")]
        public IActionResult GetIoTStatus()
        {
            try
            {
                var stats = _iotClientsService.GetConnectionStats();
                var clients = _iotClientsService.GetAllConnectedClients()
                    .Select(c => new
                    {
                        MezzoId = c.State.MezzoId,
                        IsConnected = c.IsConnected,
                        ParkingId = c.State.ParkingId,
                        BatteryLevel = c.State.BatteryLevel,
                        IsElettrico = c.State.IsElettrico,
                        IsMoving = c.State.IsMoving,
                        LockState = c.State.LockState,
                        CorsaId = c.State.CorsaId
                    }).ToList();

                return Ok(new
                {
                    Statistics = stats,
                    Clients = clients,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting IoT status");
                return StatusCode(500, new { Error = "Errore nel recupero stato IoT", Details = ex.Message });
            }
        }

        /// <summary>
        /// Testa comando di sblocco per un mezzo specifico
        /// </summary>
        [HttpPost("test-unlock/{mezzoId}")]
        public async Task<IActionResult> TestUnlockCommand(int mezzoId)
        {
            try
            {
                await _mqttActuatorService.SendUnlockCommand(mezzoId, 999); // Test Corsa ID
                
                _logger.LogInformation("🔓 Test unlock command sent for Mezzo {MezzoId}", mezzoId);
                
                return Ok(new 
                { 
                    Success = true, 
                    Message = $"Comando di sblocco inviato per Mezzo {mezzoId}",
                    MezzoId = mezzoId,
                    TestCorsaId = 999,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test unlock command for Mezzo {MezzoId}", mezzoId);
                return StatusCode(500, new { Error = "Errore nell'invio comando di sblocco", Details = ex.Message });
            }
        }

        /// <summary>
        /// Testa comando di blocco per un mezzo specifico
        /// </summary>
        [HttpPost("test-lock/{mezzoId}")]
        public async Task<IActionResult> TestLockCommand(int mezzoId)
        {
            try
            {
                await _mqttActuatorService.SendLockCommand(mezzoId, 999); // Test Corsa ID
                
                _logger.LogInformation("🔒 Test lock command sent for Mezzo {MezzoId}", mezzoId);
                
                return Ok(new 
                { 
                    Success = true, 
                    Message = $"Comando di blocco inviato per Mezzo {mezzoId}",
                    MezzoId = mezzoId,
                    TestCorsaId = 999,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test lock command for Mezzo {MezzoId}", mezzoId);
                return StatusCode(500, new { Error = "Errore nell'invio comando di blocco", Details = ex.Message });
            }
        }

        /// <summary>
        /// Verifica lo stato del sistema MQTT completo
        /// </summary>
        [HttpGet("mqtt-health")]
        public IActionResult GetMqttHealth()
        {
            try
            {
                var clientsStats = _iotClientsService.GetConnectionStats();
                var allClients = _iotClientsService.GetAllConnectedClients();
                
                var batteryWarnings = allClients
                    .Where(c => c.State.IsElettrico && c.State.BatteryLevel < 20)
                    .Select(c => new { c.State.MezzoId, c.State.BatteryLevel })
                    .ToList();

                var offlineClients = allClients
                    .Where(c => !c.IsConnected)
                    .Select(c => new { c.State.MezzoId, c.State.ParkingId })
                    .ToList();

                return Ok(new
                {
                    SystemHealth = new
                    {
                        TotalClients = allClients.Count,
                        ConnectedClients = allClients.Count(c => c.IsConnected),
                        OfflineClients = offlineClients.Count,
                        LowBatteryClients = batteryWarnings.Count,
                        MovingClients = allClients.Count(c => c.State.IsMoving),
                        Status = allClients.Count > 0 && allClients.All(c => c.IsConnected) ? "Healthy" : "Warning"
                    },
                    BatteryWarnings = batteryWarnings,
                    OfflineClients = offlineClients,
                    Statistics = clientsStats,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting MQTT health status");
                return StatusCode(500, new { Error = "Errore nel controllo stato MQTT", Details = ex.Message });
            }
        }
    }
}
