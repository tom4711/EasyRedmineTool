namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Models;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
