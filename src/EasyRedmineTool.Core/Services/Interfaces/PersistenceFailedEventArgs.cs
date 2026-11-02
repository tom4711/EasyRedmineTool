namespace EasyRedmineTool.Core.Services.Interfaces;

/// <summary>A displayable persistence error that never includes credentials or serialized settings.</summary>
public sealed class PersistenceFailedEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
