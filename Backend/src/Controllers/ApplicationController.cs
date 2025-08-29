using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fork.Adapters.Fork;
using Fork.Logic.Services.AuthenticationServices;
using Fork.Logic.Services.StateServices;
using ForkCommon.Model.Application;
using ForkCommon.Model.Privileges;
using ForkCommon.Model.Privileges.Entity.ReadEntity.ReadConsoleTab;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Fork.Controllers
{
    /// <summary>
    /// Controller for requests that affect the whole application
    /// i.e.: change app settings, create/delete server, etc.
    /// </summary>
    [ApiController]
    [Route("v1/application")]
    public class ApplicationController : AbstractRestController
    {
        private readonly ApplicationStateService _applicationState;
        private readonly AuthenticationService _authentication;
        private readonly ForkApiAdapter _forkApi;

        public ApplicationController(
            ILogger<ApplicationController> logger,
            ApplicationStateService applicationState,
            AuthenticationService authentication,
            ForkApiAdapter forkApi
        ) : base(logger)
        {
            _applicationState = applicationState;
            _authentication = authentication;
            _forkApi = forkApi;
        }

        /// <summary>
        /// Get the current application state
        /// </summary>
        [HttpGet("state")]
        [Privileges(typeof(IPrivilege))]
        public async Task<ActionResult<State>> State()
        {
            LogRequest();
            try
            {
                var state = await _applicationState.BuildAppState();
                return Ok(state);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error building application state");
                return StatusCode(500, new { error = "Failed to retrieve application state" });
            }
        }

        /// <summary>
        /// Get the current privileges for the authenticated user
        /// </summary>
        [HttpGet("privileges")]
        [Privileges(typeof(IPrivilege))]
        public ActionResult<IEnumerable<IPrivilege>> Privileges()
        {
            try
            {
                return Ok(_authentication.Privileges);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving privileges");
                return StatusCode(500, new { error = "Failed to retrieve privileges" });
            }
        }

        /// <summary>
        /// Get the external IP address for accessing the hosted servers
        /// </summary>
        [HttpGet("ip")]
        [Privileges(typeof(ReadConsoleConsoleTabPrivilege))]
        public async Task<ActionResult<string>> Ip()
        {
            try
            {
                var ip = await _forkApi.GetExternalIpAddress();
                return Ok(ip);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving external IP");
                return StatusCode(500, new { error = "Failed to retrieve external IP" });
            }
        }
    }
}
