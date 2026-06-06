namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

public class ConnectionTestService(EasyRedmineApiClient apiClient, ILogger<ConnectionTestService> logger) : IConnectionTestService
{
    private readonly EasyRedmineApiClient _apiClient = apiClient;
    private readonly ILogger<ConnectionTestService> _logger = logger;

    public async Task<ConnectionTestResult> TestConnectionAsync(ConnectionTestRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return new ConnectionTestResult { Success = false, Message = "BaseUrl fehlt." };
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new ConnectionTestResult { Success = false, Message = "API-Key fehlt." };
        }

        try
        {
            using var response = await _apiClient.GetCurrentUserAsync(request.BaseUrl, request.ApiKey, cancellationToken);

            return !response.IsSuccessStatusCode
                ? new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Verbindung fehlgeschlagen: {(int)response.StatusCode} {response.ReasonPhrase}"
                }
                : new ConnectionTestResult
            {
                Success = true,
                Message = "Verbindung erfolgreich."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verbindungstest fehlgeschlagen für {BaseUrl}.", request.BaseUrl);
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Fehler: {ex.Message}"
            };
        }
    }
}
