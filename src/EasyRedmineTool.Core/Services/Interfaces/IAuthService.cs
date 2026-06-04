using EasyRedmineTool.Core.Models;

namespace EasyRedmineTool.Core.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResult> TestConnectionAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
