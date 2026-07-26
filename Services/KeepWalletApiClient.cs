using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using KeepWalletDesktop.Models;
using Serilog;

namespace KeepWalletDesktop.Services;

public sealed class KeepWalletApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ILogger ApiLog = Log.ForContext<KeepWalletApiClient>();

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _httpClient;
    private string? _accessToken;

    public KeepWalletApiClient(string baseUrl)
    {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(normalizedBaseUrl)
        };
    }

    public string BaseUrl => _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

    public async Task<AppSettingsDto> GetAppSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/app-settings", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AppSettingsDto>(JsonOptions, cancellationToken)
            ?? new AppSettingsDto(true);
    }

    public async Task<AppSettingsDto> UpdateAppSettingsAsync(
        UpdateAppSettingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Patch, "/api/app-settings", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AppSettingsDto>(JsonOptions, cancellationToken)
            ?? new AppSettingsDto(request.RegistrationEnabled);
    }

    public async Task<AuthResponseDto> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        const string path = "/api/auth/login";
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            path,
            new LoginRequestDto(login, password),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty auth response.");

        SetAccessToken(auth.AccessToken);
        return auth;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        string username,
        string email,
        string password,
        string? fullName,
        CancellationToken cancellationToken = default)
    {
        const string path = "/api/auth/register";
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            path,
            new RegisterRequestDto(username, email, password, fullName),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty register response.");

        SetAccessToken(auth.AccessToken);
        return auth;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/auth/logout", cancellationToken);
        response.EnsureSuccessStatusCode();
        ClearSession();
    }

    public async Task<ApiUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/auth/me", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiUserDto>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/users", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiUserDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task UpdateUserAsync(
        Guid userId,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Patch, $"/api/users/{userId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ApiUserDto> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/users", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiUserDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty user response.");
    }

    public async Task<IReadOnlyList<ApiGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/groups", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiGroupDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ApiGroupDto> CreateGroupAsync(CreateGroupRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/groups", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiGroupDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty group response.");
    }

    public async Task<ApiGroupDto> UpdateGroupAsync(Guid groupId, UpdateGroupRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Patch, $"/api/groups/{groupId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiGroupDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty group response.");
    }

    public async Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/groups/{groupId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiGroupMemberDto>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, $"/api/groups/{groupId}/members", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiGroupMemberDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task AddGroupMemberAsync(Guid groupId, AddGroupMemberRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, $"/api/groups/{groupId}/members", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RemoveGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/groups/{groupId}/members/{userId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateGroupMemberRoleAsync(
        Guid groupId,
        Guid userId,
        UpdateGroupMemberRoleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Patch, $"/api/groups/{groupId}/members/{userId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task TransferGroupOwnerAsync(
        Guid groupId,
        TransferGroupOwnerRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, $"/api/groups/{groupId}/transfer-owner", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiUserDto>> SearchUsersAsync(string query, CancellationToken cancellationToken = default)
    {
        var path = $"/api/users/search?q={Uri.EscapeDataString(query)}";
        using var response = await SendAuthorizedAsync(HttpMethod.Get, path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiUserDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ApiLogDto>> GetMyLogsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/logs/me", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiLogDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/auth/refresh", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        var refresh = await response.Content.ReadFromJsonAsync<RefreshResponseDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty refresh response.");

        SetAccessToken(refresh.AccessToken);
        return true;
    }

    public async Task<IReadOnlyList<ApiTransactionDto>> GetTransactionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/transactions", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiTransactionDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ApiCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/categories", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiCategoryDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ApiBudgetDto>> GetBudgetsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/budgets", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiBudgetDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ApiBudgetDto> UpsertBudgetAsync(
        int categoryId,
        UpsertBudgetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/budgets/category/{categoryId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiBudgetDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty budget response.");
    }

    public async Task<ApiCategoryDto> CreateCategoryAsync(
        CreateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/categories", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiCategoryDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty category response.");
    }

    public async Task<ApiCategoryDto> UpdateCategoryAsync(
        int categoryId,
        UpdateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Patch, $"/api/categories/{categoryId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiCategoryDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty category response.");
    }

    public async Task<IReadOnlyList<UserCategoryPreferenceDto>> GetUserCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/user-categories", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<UserCategoryPreferenceDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task UpdateUserCategoriesAsync(
        UpdateUserCategoryPreferencesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, "/api/user-categories", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/categories/{categoryId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ApiCategoryDto> MergeCategoryAsync(
        int sourceCategoryId,
        MergeCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, $"/api/categories/{sourceCategoryId}/merge", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiCategoryDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty category response.");
    }

    public async Task<ApiTransactionDto> CreateTransactionAsync(
        CreateTransactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/transactions", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiTransactionDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty transaction response.");
    }

    public async Task<ApiTransactionDto> UpdateTransactionAsync(
        int transactionId,
        UpdateTransactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/transactions/{transactionId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiTransactionDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty transaction response.");
    }

    public async Task<ApiTransactionDto> UpdateTransactionGroupsAsync(
        int transactionId,
        ReplaceResourceGroupsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/transactions/{transactionId}/groups", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiTransactionDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty transaction response.");
    }

    public async Task DeleteTransactionAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/transactions/{transactionId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiSavingDto>> GetSavingsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/savings", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiSavingDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ApiSavingDto> CreateSavingAsync(
        CreateSavingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/savings", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiSavingDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty saving response.");
    }

    public async Task<ApiSavingDto> UpdateSavingAsync(
        int savingId,
        UpdateSavingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/savings/{savingId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiSavingDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty saving response.");
    }

    public async Task<ApiSavingDto> ShareSavingWithGroupAsync(
        int savingId,
        ShareResourceWithGroupRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/savings/{savingId}/group", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiSavingDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty saving response.");
    }

    public async Task<ApiSavingDto> UpdateSavingGroupsAsync(
        int savingId,
        ReplaceResourceGroupsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/savings/{savingId}/groups", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiSavingDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty saving response.");
    }

    public async Task DeleteSavingAsync(int savingId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/savings/{savingId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiSavingItemDto>> GetSavingItemsAsync(
        int savingId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, $"/api/savings/{savingId}/items", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiSavingItemDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ApiSavingItemDto> CreateSavingItemAsync(
        int savingId,
        CreateSavingItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, $"/api/savings/{savingId}/items", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiSavingItemDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty saving item response.");
    }

    public async Task<ApiSavingItemDto> UpdateSavingItemAsync(
        int savingId,
        int itemId,
        UpdateSavingItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/savings/{savingId}/items/{itemId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiSavingItemDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty saving item response.");
    }

    public async Task DeleteSavingItemAsync(
        int savingId,
        int itemId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/savings/{savingId}/items/{itemId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiPlannedTransactionDto>> GetPlannedTransactionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/planned-transactions", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiPlannedTransactionDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ApiTransactionDto> CreatePlannedTransactionAsync(
        CreatePlannedTransactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/planned-transactions", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiTransactionDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty planned transaction response.");
    }

    public async Task<ApiTransactionDto> UpdatePlannedTransactionAsync(
        int transactionId,
        CreatePlannedTransactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/planned-transactions/{transactionId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiTransactionDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty planned transaction response.");
    }

    public async Task<ApiTransactionDto> ConfirmPlannedTransactionAsync(
        int transactionId,
        ConfirmPlannedTransactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, $"/api/planned-transactions/{transactionId}/confirm", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiTransactionDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty transaction response.");
    }

    public async Task DeletePlannedTransactionAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/planned-transactions/{transactionId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiBankAccountDto>> GetBankAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/bank-accounts", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSession();
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ApiBankAccountDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task<ApiBankAccountDto> CreateBankAccountAsync(
        CreateBankAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/bank-accounts", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiBankAccountDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty bank account response.");
    }

    public async Task<ApiBankAccountDto> UpdateBankAccountAsync(
        Guid accountId,
        UpdateBankAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/bank-accounts/{accountId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiBankAccountDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty bank account response.");
    }

    public async Task<ApiBankAccountDto> ShareBankAccountWithGroupAsync(
        Guid accountId,
        ShareResourceWithGroupRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/bank-accounts/{accountId}/group", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiBankAccountDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty bank account response.");
    }

    public async Task<ApiBankAccountDto> UpdateBankAccountGroupsAsync(
        Guid accountId,
        ReplaceResourceGroupsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/bank-accounts/{accountId}/groups", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ApiBankAccountDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty bank account response.");
    }

    public async Task DeleteBankAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Delete, $"/api/bank-accounts/{accountId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var request = CreateRequest(method, path);
        var response = await SendRequestAsync(request, path, operation, attempt: 1, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();

        ApiLog.Information("API auth refresh required for {Operation}: {Method} {Path}", operation, method, path);
        var refreshed = await TryRefreshAsync(cancellationToken);
        if (!refreshed)
        {
            ApiLog.Warning("API auth refresh failed for {Operation}: {Method} {Path}", operation, method, path);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        ApiLog.Information("API auth refresh succeeded; retrying {Operation}: {Method} {Path}", operation, method, path);
        using var retryRequest = CreateRequest(method, path);
        return await SendRequestAsync(retryRequest, path, operation, attempt: 2, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAuthorizedJsonAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest payload,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var request = CreateRequest(method, path);
        request.Content = JsonContent.Create(payload);
        var response = await SendRequestAsync(request, path, operation, attempt: 1, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();

        ApiLog.Information("API auth refresh required for {Operation}: {Method} {Path}", operation, method, path);
        var refreshed = await TryRefreshAsync(cancellationToken);
        if (!refreshed)
        {
            ApiLog.Warning("API auth refresh failed for {Operation}: {Method} {Path}", operation, method, path);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        ApiLog.Information("API auth refresh succeeded; retrying {Operation}: {Method} {Path}", operation, method, path);
        using var retryRequest = CreateRequest(method, path);
        retryRequest.Content = JsonContent.Create(payload);
        return await SendRequestAsync(retryRequest, path, operation, attempt: 2, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var request = new HttpRequestMessage(method, path);
        return await SendRequestAsync(request, path, operation, attempt: 1, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendJsonAsync<TRequest>(
        HttpMethod method,
        string path,
        TRequest payload,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload)
        };

        return await SendRequestAsync(request, path, operation, attempt: 1, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        string path,
        string operation,
        int attempt,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = request.Method.Method;

        ApiLog.Debug(
            "API start {Operation}: {Method} {Path} (base: {BaseUrl}, attempt: {Attempt})",
            operation,
            method,
            path,
            BaseUrl,
            attempt);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var level = response.IsSuccessStatusCode ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Warning;
            ApiLog.Write(
                level,
                "API completed {Operation}: {Method} {Path} -> {StatusCode} {ReasonPhrase} in {ElapsedMs} ms (attempt: {Attempt})",
                operation,
                method,
                path,
                (int)response.StatusCode,
                response.ReasonPhrase,
                stopwatch.ElapsedMilliseconds,
                attempt);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            ApiLog.Error(
                ex,
                "API failed {Operation}: {Method} {Path} after {ElapsedMs} ms (attempt: {Attempt})",
                operation,
                method,
                path,
                stopwatch.ElapsedMilliseconds,
                attempt);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            ApiLog.Warning(
                ex,
                "API canceled {Operation}: {Method} {Path} after {ElapsedMs} ms (attempt: {Attempt})",
                operation,
                method,
                path,
                stopwatch.ElapsedMilliseconds,
                attempt);
            throw;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            response.EnsureSuccessStatusCode();
            return;
        }

        throw new HttpRequestException(
            ExtractErrorMessage(content) ?? $"API request failed with status {(int)response.StatusCode}: {content}",
            null,
            response.StatusCode);
    }

    private static string? ExtractErrorMessage(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }

            if (document.RootElement.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private void SetAccessToken(string accessToken)
    {
        _accessToken = accessToken;
    }

    private void ClearSession()
    {
        _accessToken = null;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("API base URL is not configured.");
        }

        return baseUrl.TrimEnd('/');
    }
}
