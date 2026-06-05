using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

namespace EasyRedmineTool.Core.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string baseUrl = "https://projects.hawe.com/";

    [ObservableProperty]
    private string apiKey = "REDACTED";

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
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Verbindung wird geprüft ...";

            var result = await _authService.TestConnectionAsync(new LoginRequest
            {
                BaseUrl = BaseUrl,
                ApiKey = ApiKey
            });

            StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
