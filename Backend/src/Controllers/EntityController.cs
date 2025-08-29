using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fork.Logic.Managers;
using Fork.Logic.Services.EntityServices;
using ForkCommon.Model.Entity.Pocos;
using ForkCommon.Model.Payloads.Entity;
using ForkCommon.Model.Privileges;
using ForkCommon.Model.Privileges.Application;
using ForkCommon.Model.Privileges.Entity.ReadEntity.ReadConsoleTab;
using ForkCommon.Model.Privileges.Entity.ReadEntity.ReadSettingsTab;
using ForkCommon.Model.Privileges.Entity.WriteEntity.WriteConsoleTab;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Fork.Controllers
{
    /// <summary>
    /// Controller for requests that affect a single entity
    /// i.e: start/stop server, change server settings,...
    /// </summary>
    public class EntityController(
        ILogger<EntityController> logger,
        EntityManager entityManager,
        EntityService entityService,
        ServerService serverService,
        CommandService commandService,
        EntitySettingsService entitySettingsService)
        : AbstractRestController(logger)
    {
        #region DTOs
        public class ConsoleMessageDto
        {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; }
        }

        public class SettingsDto
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
        #endregion

        [HttpPost("createServer")]
        [Privileges(typeof(CreateEntityPrivilege))]
        public async Task<ulong> CreateServer([FromBody] CreateServerPayload abstractPayload)
        {
            ulong result = await serverService.CreateServerAsync(
                abstractPayload.ServerName,
                abstractPayload.ServerVersion,
                abstractPayload.VanillaSettings,
                abstractPayload.JavaSettings,
                abstractPayload.WorldPath
            );

            if (result != 0)
            {
                await entityService.UpdateEntityListAsync();
            }

            return result;
        }

        [HttpPost("{entityId}/delete")]
        [Privileges(typeof(DeleteEntityPrivilege))]
        public async Task<StatusCodeResult> DeleteEntity([FromRoute] ulong entityId)
        {
            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return BadRequest();

            await entityService.DeleteEntityAsync(entity);
            await entityService.UpdateEntityListAsync();
            return Ok();
        }

        [HttpPost("{entityId}/start")]
        [Privileges(typeof(WriteConsoleTabPrivilege))]
        public async Task<StatusCodeResult> StartEntity([FromRoute] ulong entityId)
        {
            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return BadRequest();

            await entityService.StartEntityAsync(entity);
            return Ok();
        }

        [HttpPost("{entityId}/stop")]
        [Privileges(typeof(WriteConsoleTabPrivilege))]
        public async Task<StatusCodeResult> StopEntity([FromRoute] ulong entityId)
        {
            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return BadRequest();

            await entityService.StopEntityAsync(entity);
            return Ok();
        }

        [HttpPost("{entityId}/restart")]
        [Privileges(typeof(WriteConsoleTabPrivilege))]
        public async Task<StatusCodeResult> RestartEntity([FromRoute] ulong entityId)
        {
            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return BadRequest();

            await entityService.RestartEntityAsync(entity);
            return Ok();
        }

        [Consumes("text/plain")]
        [HttpPost("{entityId}/consoleIn")]
        [Privileges(typeof(WriteConsoleTabPrivilege))]
        public async Task<StatusCodeResult> ConsoleIn([FromBody] string message, [FromRoute] ulong entityId)
        {
            if (string.IsNullOrEmpty(message) || message == "/") return BadRequest();

            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return BadRequest();

            entity.ConsoleHandler?.Invoke(message);
            return Ok();
        }

        [HttpGet("{entityId}/console")]
        [Privileges(typeof(ReadConsoleConsoleTabPrivilege))]
        public async Task<List<ConsoleMessageDto>> Console([FromRoute] ulong entityId)
        {
            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return new List<ConsoleMessageDto>();

            int count = Math.Min(entity.ConsoleMessages.Count, 1000);
            return entity.ConsoleMessages
                .GetRange(entity.ConsoleMessages.Count - count, count)
                .Select(cm => new ConsoleMessageDto
                {
                    Timestamp = cm.Timestamp,
                    Message = cm.Message
                })
                .ToList();
        }

        [HttpGet("{entityId}/commands")]
        [Privileges(typeof(ReadConsoleConsoleTabPrivilege))]
        public async Task<Command?> Commands([FromRoute] ulong entityId)
        {
            var entity = await entityManager.EntityById(entityId);
            if (entity == null) return null;

            return await commandService.GetCommandTreeForEntity(entity);
        }

        [HttpGet("{entityId}/settings")]
        [Privileges(typeof(ReadGeneralSettingsTabPrivilege))]
        public async Task<List<SettingsDto>> GetSettingsForEntity([FromRoute] ulong entityId)
        {
            var settings = await entitySettingsService.GetAllSettingsForEntity(entityId);
            return settings.Select(s => new SettingsDto
            {
                Name = s.Name,
                Value = s.Value
            }).ToList();
        }
    }
}
