using SharingMezzi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SharingMezzi.Infrastructure.Services
{
    /// <summary>
    /// Implementazione del servizio per inviare comandi agli attuatori MQTT
    /// Collega il CorsaService al broker MQTT per gestire sblocco/blocco mezzi
    /// </summary>
    public class MqttActuatorService : IMqttActuatorService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<MqttActuatorService> _logger;

        public MqttActuatorService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<MqttActuatorService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Invia comando di sblocco mezzo via MQTT
        /// </summary>
        public async Task SendUnlockCommand(int mezzoId, int? corsaId = null)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                
                // Cerca il broker MQTT nei servizi registrati
                var brokerService = scope.ServiceProvider.GetService<SharingMezziBroker>();
                if (brokerService != null)
                {
                    await brokerService.SendUnlockCommand(mezzoId, corsaId);
                    _logger.LogInformation("Successfully sent unlock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                        mezzoId, corsaId);
                }
                else
                {
                    _logger.LogWarning("⚠️ MQTT Broker service not found - cannot send unlock command for Mezzo {MezzoId}", 
                        mezzoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending unlock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                    mezzoId, corsaId);
                throw;
            }
        }

        /// <summary>
        /// Invia comando di blocco mezzo via MQTT
        /// </summary>
        public async Task SendLockCommand(int mezzoId, int? corsaId = null)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                
                // Cerca il broker MQTT nei servizi registrati
                var brokerService = scope.ServiceProvider.GetService<SharingMezziBroker>();
                if (brokerService != null)
                {
                    await brokerService.SendLockCommand(mezzoId, corsaId);
                    _logger.LogInformation("Successfully sent lock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                        mezzoId, corsaId);
                }
                else
                {
                    _logger.LogWarning("⚠️ MQTT Broker service not found - cannot send lock command for Mezzo {MezzoId}", 
                        mezzoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending lock command for Mezzo {MezzoId}, Corsa {CorsaId}", 
                    mezzoId, corsaId);
                throw;
            }
        }
    }
}
