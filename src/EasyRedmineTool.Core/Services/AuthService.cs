using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

namespace EasyRedmineTool.Core.Services;

public class AuthService : IAuthService
{
    private readonly EasyRedmineApiClient _apiClient;

    public AuthService(EasyRedmineApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<LoginResult> TestConnectionAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return new LoginResult { Success = false, Message = "BaseUrl fehlt." };
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new LoginResult { Success = false, Message = "API-Key fehlt." };
        }

        try
        {
            using var response = await _apiClient.GetCurrentUserAsync(request.BaseUrl, request.ApiKey, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = $"Verbindung fehlgeschlagen: {(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }

            return new LoginResult
            {
                Success = true,
                Message = "Verbindung erfolgreich."
            };
        }
        catch (Exception ex)
        {
            return new LoginResult
            {
                Success = false,
                Message = $"Fehler: {ex.Message}"
            };
        }
    }
}
