using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using KeepWalletDesktop.Services;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuthPasswordChar))]
    [NotifyPropertyChangedFor(nameof(AuthPasswordVisibilityIcon))]
    private bool isAuthPasswordVisible;

    public string AuthCardTitle => IsRegisterMode ? Texts.RegisterTitle : Texts.LoginTitle;
    public bool IsLoginMode => !IsRegisterMode;
    public bool CanOpenRegisterMode => IsRegistrationEnabled;
    public bool CanShowAuthFooterAction => IsRegisterMode || IsRegistrationEnabled;
    public string AuthFooterPrompt => IsRegisterMode ? Texts.AlreadyRegisteredPrompt : Texts.NotRegisteredPrompt;
    public string AuthFooterAction => IsRegisterMode ? Texts.SwitchToLoginLink : Texts.SwitchToRegisterLink;
    public char AuthPasswordChar => IsAuthPasswordVisible ? '\0' : '*';
    public string AuthPasswordVisibilityIcon => IsAuthPasswordVisible ? "\uE220" : "\uE222";

    [RelayCommand]
    private void OpenAuth()
    {
        CurrentPage = AuthPage;
        _ = LoadAppSettingsAsync();
    }

    [RelayCommand]
    private void ToggleAuthMode()
    {
        if (!IsRegisterMode && !IsRegistrationEnabled)
        {
            return;
        }

        IsRegisterMode = !IsRegisterMode;
        ApiStatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ToggleAuthPasswordVisibility()
    {
        IsAuthPasswordVisible = !IsAuthPasswordVisible;
    }

    [RelayCommand]
    private void OpenServerDialog()
    {
        LoadServerSettingsFromBaseUrl();
        IsServerDialogOpen = true;
    }

    private async Task LoadAppSettingsAsync()
    {
        try
        {
            EnsureApiClient();
            var settings = await _apiClient!.GetAppSettingsAsync();
            IsRegistrationEnabled = settings.RegistrationEnabled;
        }
        catch (Exception)
        {
            IsRegistrationEnabled = true;
        }
    }

    [RelayCommand]
    private void CloseServerDialog()
    {
        IsServerDialogOpen = false;
    }

    [RelayCommand]
    private void SaveServerSettings()
    {
        var host = ServerHost.Trim();
        var port = ServerPort.Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            ApiStatusMessage = Texts.ApiBaseUrlMissing;
            return;
        }

        if (!Uri.TryCreate(host, UriKind.Absolute, out var hostUri) &&
            !Uri.TryCreate($"http://{host}", UriKind.Absolute, out hostUri))
        {
            ApiStatusMessage = Texts.ApiBaseUrlMissing;
            return;
        }

        var builder = new UriBuilder(hostUri);

        if (!string.IsNullOrWhiteSpace(port))
        {
            if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
            {
                ApiStatusMessage = Texts.ApiBaseUrlMissing;
                return;
            }

            builder.Port = parsedPort;
        }

        ApiBaseUrl = builder.Uri.GetLeftPart(UriPartial.Authority);
        _apiClient?.Dispose();
        _apiClient = null;
        IsServerDialogOpen = false;
        SaveLocalUserSettings();
        ApiStatusMessage = $"{Texts.ApiConnectionTitle}: {ApiBaseUrl}";
    }

    [RelayCommand]
    private async Task LoginToApiAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiLogin) || string.IsNullOrWhiteSpace(ApiPassword))
        {
            ApiStatusMessage = Texts.LoginPasswordRequired;
            CurrentPage = AuthPage;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            ApiStatusMessage = Texts.AuthLoginState;

            var auth = await _apiClient!.LoginAsync(ApiLogin.Trim(), ApiPassword);
            ApplyAuthorizedUser(auth.UserId, auth.Username, auth.Email, auth.RoleName);
            SaveLocalUserSettings();
            ApiPassword = string.Empty;
            ApiStatusMessage = $"{Texts.LoginSuccessPrefix}: {auth.Username}.";
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadGroupsAsync();
            await LoadBankAccountsAsync();
            if (BankAccounts.Count == 0)
            {
                IsCategorySettingsMode = false;
                await PrepareOnboardingAsync();
                return;
            }

            await LoadTransactionsAsync();
        }
        catch (HttpRequestException ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = GetAuthLoginErrorMessage(ex);
        }
        catch (Exception)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = Texts.AuthNoServerConnection;
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterInApiAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RegisterUsername) ||
            string.IsNullOrWhiteSpace(RegisterEmail) ||
            string.IsNullOrWhiteSpace(RegisterPassword))
        {
            ApiStatusMessage = Texts.RegisterRequired;
            CurrentPage = AuthPage;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            ApiStatusMessage = Texts.RegisteringToApi;

            var auth = await _apiClient!.RegisterAsync(
                RegisterUsername.Trim(),
                RegisterEmail.Trim(),
                RegisterPassword,
                string.IsNullOrWhiteSpace(RegisterFullName) ? null : RegisterFullName.Trim());

            ApplyAuthorizedUser(auth.UserId, auth.Username, auth.Email, auth.RoleName);
            ApiLogin = RegisterUsername.Trim();
            SaveLocalUserSettings();
            ApiPassword = string.Empty;
            RegisterPassword = string.Empty;
            ApiStatusMessage = $"{Texts.RegisterSuccessPrefix}: {auth.Username}.";
            await LoadCategoriesAsync();
            await PrepareOnboardingAsync();
        }
        catch (HttpRequestException ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = $"{Texts.RegisterErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshApiProfileAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        await RestoreApiSessionAsync();
    }

    [RelayCommand]
    private async Task LogoutFromApiAsync()
    {
        if (IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.LogoutAsync();
            ResetAuthorizedUser();
            ApiStatusMessage = Texts.ApiSessionClosed;
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.ApiLogoutErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task RestoreApiSessionAsync()
    {
        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            var user = await _apiClient!.GetCurrentUserAsync();
            if (user is null)
            {
                ResetAuthorizedUser();
                ApiStatusMessage = string.Format(Texts.ApiAvailableLoginRequired, ApiBaseUrl);
                return;
            }

            ApplyAuthorizedUser(user.Id, user.Username, user.Email, user.RoleName);
            ApiStatusMessage = $"{Texts.SessionActivePrefix}: {user.Username}.";
            await LoadCategoriesAsync();
            await LoadUserCategoryPreferencesAsync();
            await LoadTransactionsAsync();
            await LoadSavingsAsync();
        }
        catch (HttpRequestException ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ResetAuthorizedUser();
            ApiStatusMessage = $"{Texts.ApiUnavailablePrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private bool EnsureAuthorizedNavigation()
    {
        if (IsApiAuthorized)
        {
            return true;
        }

        CurrentPage = AuthPage;
        ApiStatusMessage = Texts.AuthorizationRequired;
        return false;
    }

    private void ApplyAuthorizedUser(Guid userId, string username, string email, string? roleName)
    {
        CurrentUserId = userId;
        CurrentUsername = username;
        CurrentUserEmail = email;
        CurrentUserRole = roleName ?? Texts.DefaultRole;
        IsApiAuthorized = true;
        IsRegisterMode = false;
        CurrentPage = HomePage;
        LoadApiOnlyPlaceholders();
        RefreshLocalizedProperties();
    }

    private void ResetAuthorizedUser()
    {
        CurrentUsername = Texts.GuestLabel;
        CurrentUserId = null;
        CurrentUserEmail = string.Empty;
        CurrentUserRole = Texts.DefaultRole;
        IsApiAuthorized = false;
        CurrentPage = AuthPage;
        LoadApiOnlyPlaceholders();
        SavingsStatusMessage = Texts.SavingsOperationsDisabled;
        RefreshLocalizedProperties();
    }

    private void EnsureApiClient()
    {
        var normalizedBaseUrl = ApiBaseUrl.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            throw new InvalidOperationException(Texts.ApiBaseUrlMissing);
        }

        if (_apiClient is not null &&
            string.Equals(_apiClient.BaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _apiClient?.Dispose();
        _apiClient = new KeepWalletApiClient(normalizedBaseUrl);
    }

    private string GetApiErrorMessage(HttpRequestException ex)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Texts.UnauthorizedError,
            HttpStatusCode.Conflict => Texts.ConflictError,
            HttpStatusCode.BadRequest => $"{Texts.ValidationErrorPrefix}: {ex.Message}",
            _ => $"{Texts.RequestErrorPrefix}: {ex.Message}"
        };
    }

    private string GetAuthLoginErrorMessage(HttpRequestException ex)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Texts.AuthInvalidCredentials,
            null => Texts.AuthNoServerConnection,
            _ => GetApiErrorMessage(ex)
        };
    }

    private void LoadServerSettingsFromBaseUrl()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri))
        {
            ServerHost = "http://localhost";
            ServerPort = "5174";
            return;
        }

        ServerHost = $"{uri.Scheme}://{uri.Host}";
        ServerPort = uri.IsDefaultPort ? string.Empty : uri.Port.ToString(CultureInfo.InvariantCulture);
    }

    private void SaveLocalUserSettings()
    {
        UserSettingsStore.Save(new UserSettings
        {
            ApiBaseUrl = ApiBaseUrl.Trim().TrimEnd('/'),
            LastLogin = ApiLogin.Trim(),
            LanguageCode = SelectedLanguage?.Code ?? "ua",
            ThemeName = SelectedTheme?.Code ?? ThemeManager.Light
        });
    }
}
