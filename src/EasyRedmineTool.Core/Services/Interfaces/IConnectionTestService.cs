namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Models;

public interface IConnectionTestService
{
    Task<ConnectionTestResult> TestConnectionAsync(ConnectionTestRequest request, CancellationToken cancellationToken = default);
}
