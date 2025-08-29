using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fork.Logic.Managers;
using ForkCommon.Model.Application;

namespace Fork.Logic.Services.StateServices
{
    /// <summary>
    /// Service to build the application state for frontend consumption
    /// </summary>
    public class ApplicationStateService
    {
        private readonly ApplicationManager _applicationManager;
        private readonly EntityManager _entityManager;

        public ApplicationStateService(ApplicationManager applicationManager, EntityManager entityManager)
        {
            _applicationManager = applicationManager;
            _entityManager = entityManager;
        }

        /// <summary>
        /// Builds the full application state with concrete DTOs for serialization
        /// </summary>
        public async Task<StateDto> BuildAppState()
        {
            var entities = await _entityManager.ListAllEntities();

            var dtoList = entities.Select(e => new EntityDto
            {
                Id = e.Id,
                Name = e.Name ?? string.Empty,
                Status = e.Status?.ToString() ?? "Unknown",
                Version = e.Version?.ToString() ?? "Unknown"
            }).ToList();

            return new StateDto(dtoList);
        }
    }

    /// <summary>
    /// DTO version of State for sending to frontend
    /// </summary>
    public class StateDto
    {
        public StateDto(List<EntityDto> entities)
        {
            Entities = entities;
        }

        public List<EntityDto> Entities { get; set; }
    }

    /// <summary>
    /// DTO for safely serializing IEntity
    /// </summary>
    public class EntityDto
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public string Version { get; set; } = "Unknown";
    }
}
