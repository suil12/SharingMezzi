using SharingMezzi.Core.DTOs;
using SharingMezzi.Core.Entities;
using SharingMezzi.Core.Interfaces.Repositories;
using SharingMezzi.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace SharingMezzi.Core.Services
{
    public class MezzoService : IMezzoService
    {
        private readonly IMezzoRepository _mezzoRepository;
        private readonly ILogger<MezzoService> _logger;

        public MezzoService(IMezzoRepository mezzoRepository, ILogger<MezzoService> logger)
        {
            _mezzoRepository = mezzoRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<MezzoDto>> GetAllMezziAsync()
        {
            var mezzi = await _mezzoRepository.GetAllAsync();
            return mezzi.Select(MapToDto);
        }

        public async Task<IEnumerable<MezzoDto>> GetMezziDisponibiliAsync()
        {
            var mezzi = await _mezzoRepository.GetAvailableAsync();
            return mezzi.Select(MapToDto);
        }

        public async Task<MezzoDto?> GetMezzoByIdAsync(int id)
        {
            var mezzo = await _mezzoRepository.GetByIdAsync(id);
            return mezzo != null ? MapToDto(mezzo) : null;
        }

        public async Task<IEnumerable<MezzoDto>> GetMezziByParcheggioAsync(int parcheggioId)
        {
            var mezzi = await _mezzoRepository.GetByParcheggioAsync(parcheggioId);
            return mezzi.Select(MapToDto);
        }

        public async Task<MezzoDto> CreateMezzoAsync(CreateMezzoDto createDto)
        {            var mezzo = new Mezzo
            {
                Modello = createDto.Modello,
                Tipo = Enum.Parse<TipoMezzo>(createDto.Tipo),
                IsElettrico = createDto.IsElettrico,
                TariffaPerMinuto = createDto.TariffaPerMinuto,
                TariffaFissa = 1.00m, // Tariffa fissa standard
                ParcheggioId = createDto.ParcheggioId,
                SlotId = createDto.SlotId,
                LivelloBatteria = createDto.IsElettrico ? 100 : null,
                Stato = StatoMezzo.Disponibile
            };

            mezzo = await _mezzoRepository.AddAsync(mezzo);
            _logger.LogInformation("Created new mezzo {MezzoId}: {Modello}", mezzo.Id, mezzo.Modello);
            
            return MapToDto(mezzo);
        }

        public async Task<MezzoDto> UpdateMezzoAsync(int id, MezzoDto mezzoDto)
        {
            var mezzo = await _mezzoRepository.GetByIdAsync(id);
            if (mezzo == null)
                throw new ArgumentException($"Mezzo {id} not found");

            mezzo.Modello = mezzoDto.Modello;
            mezzo.TariffaPerMinuto = mezzoDto.TariffaPerMinuto;
            mezzo.ParcheggioId = mezzoDto.ParcheggioId;
            mezzo.SlotId = mezzoDto.SlotId;
            
            if (Enum.TryParse<StatoMezzo>(mezzoDto.Stato, out var stato))
                mezzo.Stato = stato;

            mezzo = await _mezzoRepository.UpdateAsync(mezzo);
            return MapToDto(mezzo);
        }

        public async Task DeleteMezzoAsync(int id)
        {
            await _mezzoRepository.DeleteAsync(id);
            _logger.LogInformation("Deleted mezzo {MezzoId}", id);
        }

        public async Task UpdateBatteryAsync(int mezzoId, int batteryLevel)
        {
            await _mezzoRepository.UpdateBatteryLevelAsync(mezzoId, batteryLevel);
            _logger.LogDebug("Updated battery for mezzo {MezzoId}: {BatteryLevel}%", mezzoId, batteryLevel);
        }

        public async Task<bool> IsMezzoAvailableAsync(int mezzoId)
        {
            var mezzo = await _mezzoRepository.GetByIdAsync(mezzoId);
            return mezzo?.Stato == StatoMezzo.Disponibile;
        }

        private static MezzoDto MapToDto(Mezzo mezzo)
        {            return new MezzoDto
            {
                Id = mezzo.Id,
                Modello = mezzo.Modello,
                Tipo = mezzo.Tipo.ToString(),
                IsElettrico = mezzo.IsElettrico,
                Stato = mezzo.Stato.ToString(),
                LivelloBatteria = mezzo.LivelloBatteria,
                TariffaPerMinuto = mezzo.TariffaPerMinuto,
                TariffaFissa = mezzo.TariffaFissa,
                UltimaManutenzione = mezzo.UltimaManutenzione,
                ParcheggioId = mezzo.ParcheggioId,
                SlotId = mezzo.SlotId
            };
        }
    }
}