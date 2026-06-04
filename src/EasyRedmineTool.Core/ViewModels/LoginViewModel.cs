using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

namespace EasyRedmineTool.Core.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (isBusy)
            return;

        try
        {
            isBusy = true;
            statusMessage = "Verbindung wird geprüft ...";

            var result = await _authService.TestConnectionAsync(new LoginRequest
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey
            });

            statusMessage = result.Message;
        }
        finally
        {
            isBusy = false;
        }
    }
}
