namespace EasyRedmineTool.Desktop.ViewModels;

using EasyRedmineTool.Core.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(LoginViewModel loginViewModel)
    {
        LoginViewModel = loginViewModel;
    }

    public LoginViewModel LoginViewModel { get; }
}
