using ForkCommon.ExtensionMethods;
using ForkCommon.Model.Application;
using ForkCommon.Model.Application.Exceptions;
using ForkFrontend.Logic.Services.HttpsClients;
using ForkFrontend.Logic.Services.Managers;

namespace ForkFrontend.Logic.Services.Connections;

public class ApplicationConnectionService : AbstractConnectionService
{
    public ApplicationConnectionService(ILogger<ApplicationConnectionService> logger, BackendClient client,
        ToastManager toastManager) : base(logger, client, toastManager)
    {
    }

    /// <summary>
    /// Get the main application state from the backend.
    /// This should only be called once in the best case and then updated via WebSocket events.
    /// </summary>
    public async Task<State> GetApplicationState()
    {
        Logger.LogDebug("Loading main state");

        HttpResponseMessage responseMessage = await Client.GetAsync("/v1/application/state");
        string message = await responseMessage.Content.ReadAsStringAsync();

        if (!responseMessage.IsSuccessStatusCode)
        {
            // Log full response to help debug server-side issues
            Logger.LogError("Failed to get application state. Status: {StatusCode}, Body: {Body}", 
                            responseMessage.StatusCode, message);
            throw new ForkException($"Server returned {responseMessage.StatusCode}: {message}");
        }

        try
        {
            State? result = message.FromJson<State>();
            if (result == null)
            {
                throw new ForkException("Invalid response from server: deserialized state was null");
            }

            return result;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error deserializing application state. Response body: {Body}", message);
            throw new ForkException("Invalid response from server", e);
        }
    }

    public async Task<string> GetIpAddress()
    {
        Logger.LogDebug("Getting server's external IP address");

        HttpResponseMessage responseMessage = await Client.GetAsync("/v1/application/ip");
        string body = await responseMessage.Content.ReadAsStringAsync();

        if (!responseMessage.IsSuccessStatusCode)
        {
            Logger.LogError("Failed to get IP. Status: {StatusCode}, Body: {Body}",
                            responseMessage.StatusCode, body);
            throw new ForkException($"Server returned {responseMessage.StatusCode}: {body}");
        }

        return body;
    }
}
