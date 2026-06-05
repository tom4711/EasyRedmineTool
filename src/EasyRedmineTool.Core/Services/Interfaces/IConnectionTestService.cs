using EasyRedmineTool.Core.Models;

namespace EasyRedmineTool.Core.Services.Interfaces;

public interface IConnectionTestService
{
    Task<ConnectionTestResult> TestConnectionAsync(ConnectionTestRequest request, CancellationToken cancellationToken = default);
}
