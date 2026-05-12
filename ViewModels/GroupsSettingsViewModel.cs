using CommunityToolkit.Mvvm.Input;
using KeepWalletDesktop.Models;
using KeepWalletDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KeepWalletDesktop.ViewModels;

public partial class MainViewModel
{

    private async Task LoadGroupsAsync()
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        try
        {
            EnsureApiClient();
            var groups = await _apiClient!.GetGroupsAsync();
            Groups = new ObservableCollection<GroupItemViewModel>(
                groups.Select(g => new GroupItemViewModel(
                    g.Id,
                    g.Name,
                    g.RoleName,
                    g.MemberCount,
                    g.CreatedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))));
            SelectedAccessGroup ??= Groups.FirstOrDefault();
            if (SelectedAccessGroup is not null)
            {
                await LoadSelectedGroupMembersAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    partial void OnSelectedAccessGroupChanged(GroupItemViewModel? value)
    {
        _ = LoadSelectedGroupMembersAsync();
    }

    partial void OnSelectedGroupUserChanged(UserListItemViewModel? value)
    {
        if (value is not null)
        {
            GroupMemberLoginInput = value.Email;
        }
    }

    private async Task LoadSelectedGroupMembersAsync()
    {
        if (!IsApiAuthorized || SelectedAccessGroup is null)
        {
            SelectedGroupMembers = [];
            return;
        }

        try
        {
            EnsureApiClient();
            var members = await _apiClient!.GetGroupMembersAsync(SelectedAccessGroup.Id);
            SelectedGroupMembers = new ObservableCollection<GroupMemberItemViewModel>(
                members.Select(m => new GroupMemberItemViewModel(
                    m.GroupId,
                    m.UserId,
                    m.Username,
                    m.FullName ?? "-",
                    m.RoleName,
                    m.JoinedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))));
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SearchGroupUsersAsync()
    {
        try
        {
            EnsureApiClient();
            var users = await _apiClient!.SearchUsersAsync(GroupMemberLoginInput);
            var existingUserIds = SelectedGroupMembers.Select(m => m.UserId).ToHashSet();
            GroupUserSearchResults = new ObservableCollection<UserListItemViewModel>(
                users
                    .Where(u => !existingUserIds.Contains(u.Id))
                    .Select(u => new UserListItemViewModel(
                        u.Id,
                        u.Username,
                        u.Email,
                        u.FullName ?? "-",
                        u.RoleName ?? Texts.DefaultRole,
                        u.IsActive,
                        u.CreatedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))));
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddGroupMemberAsync()
    {
        if (IsApiBusy || SelectedAccessGroup is null || string.IsNullOrWhiteSpace(GroupMemberLoginInput))
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.AddGroupMemberAsync(
                SelectedAccessGroup.Id,
                new AddGroupMemberRequestDto(GroupMemberLoginInput.Trim(), SelectedGroupMemberRole));
            GroupMemberLoginInput = string.Empty;
            SelectedGroupUser = null;
            GroupUserSearchResults = [];
            await LoadGroupsAsync();
            await LoadSelectedGroupMembersAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveGroupMemberAsync(GroupMemberItemViewModel? member)
    {
        if (IsApiBusy || SelectedAccessGroup is null || member is null)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.RemoveGroupMemberAsync(SelectedAccessGroup.Id, member.UserId);
            await LoadGroupsAsync();
            await LoadSelectedGroupMembersAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShareBankAccountWithGroupAsync()
    {
        if (IsApiBusy || SelectedSharedBankAccount is null || SelectedAccessGroup is null)
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.ShareBankAccountWithGroupAsync(
                SelectedSharedBankAccount.Id,
                new ShareResourceWithGroupRequestDto(SelectedAccessGroup.Id));
            await LoadBankAccountsAsync();
            await LoadTransactionsAsync();
            await LoadScheduledPaymentsAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShareSavingWithGroupAsync()
    {
        if (IsApiBusy || SelectedSharedSaving is null || SelectedAccessGroup is null)
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.ShareSavingWithGroupAsync(
                SelectedSharedSaving.Id,
                new ShareResourceWithGroupRequestDto(SelectedAccessGroup.Id));
            await LoadSavingsAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        if (IsApiBusy || string.IsNullOrWhiteSpace(NewGroupNameInput))
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var createdGroup = await _apiClient!.CreateGroupAsync(new CreateGroupRequestDto(NewGroupNameInput.Trim()));
            NewGroupNameInput = string.Empty;
            await LoadGroupsAsync();
            SelectedAccessGroup = Groups.FirstOrDefault(g => g.Id == createdGroup.Id) ?? SelectedAccessGroup;
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task LoadSettingsDataAsync()
    {
        if (!IsApiAuthorized)
        {
            return;
        }

        await LoadUserLogsAsync();

        if (IsCurrentUserAdmin)
        {
            await LoadAdminUsersAsync();
        }
        else
        {
            AdminUsers = [];
        }
    }

    private async Task LoadAdminUsersAsync()
    {
        try
        {
            EnsureApiClient();
            var users = await _apiClient!.GetUsersAsync();
            AdminUsers = new ObservableCollection<UserListItemViewModel>(
                users.Select(u => new UserListItemViewModel(
                    u.Id,
                    u.Username,
                    u.Email,
                    u.FullName ?? "-",
                    u.RoleName ?? Texts.DefaultRole,
                    u.IsActive,
                    u.CreatedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))));
            OnPropertyChanged(nameof(AdminUsersCountDisplay));
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleUserAccessAsync(UserListItemViewModel? user)
    {
        if (user is null || IsApiBusy || !IsCurrentUserAdmin)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.UpdateUserAsync(user.Id, new UpdateUserRequestDto(IsActive: !user.IsActive));
            await LoadAdminUsersAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
        }
    }

    private async Task LoadUserLogsAsync()
    {
        try
        {
            EnsureApiClient();
            var logs = await _apiClient!.GetMyLogsAsync();
            UserLogs = new ObservableCollection<AuditLogItemViewModel>(
                logs.Select(l => new AuditLogItemViewModel(
                    l.Id,
                    l.Username ?? "-",
                    l.Action,
                    l.EntityType ?? "-",
                    FormatAuditLog(l),
                    l.Device ?? "-",
                    l.CreatedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))));
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    private static string FormatAuditLog(ApiLogDto log)
    {
        if (string.IsNullOrWhiteSpace(log.Details))
        {
            return $"{log.Username ?? "User"}: {log.Action}";
        }

        try
        {
            using var document = JsonDocument.Parse(log.Details);
            var root = document.RootElement;
            var username = log.Username ?? "User";

            if (string.Equals(log.EntityType, "transactions", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(log.Action, "UPDATE_TRANSACTIONS", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("old_data", out var oldData) &&
                root.TryGetProperty("new_data", out var newData))
            {
                var transactionId = GetJsonValue(newData, "id");
                var changes = BuildChangedFields(oldData, newData, new Dictionary<string, string>
                {
                    ["category_id"] = "category",
                    ["amount"] = "price",
                    ["transaction_date"] = "date",
                    ["description"] = "description",
                    ["account_id"] = "account"
                });

                return changes.Count == 0
                    ? $"User {username} updated transaction {transactionId}."
                    : $"User {username} changed {string.Join(", ", changes.Select(c => c.Field))} of transaction {transactionId}: {string.Join("; ", changes.Select(c => $"{c.Field} from {c.OldValue} to {c.NewValue}"))}.";
            }

            if (root.TryGetProperty("new_data", out var createdData))
            {
                return $"User {username} created {log.EntityType} {GetJsonValue(createdData, "id")}.";
            }

            if (root.TryGetProperty("deleted_data", out var deletedData))
            {
                return $"User {username} deleted {log.EntityType} {GetJsonValue(deletedData, "id")}.";
            }
        }
        catch (JsonException)
        {
            return log.Details ?? string.Empty;
        }

        return $"{log.Username ?? "User"}: {log.Action}";
    }

    private static List<(string Field, string OldValue, string NewValue)> BuildChangedFields(
        JsonElement oldData,
        JsonElement newData,
        IReadOnlyDictionary<string, string> fieldNames)
    {
        var changes = new List<(string Field, string OldValue, string NewValue)>();

        foreach (var (jsonName, displayName) in fieldNames)
        {
            var oldValue = GetJsonValue(oldData, jsonName);
            var newValue = GetJsonValue(newData, jsonName);
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                changes.Add((displayName, oldValue, newValue));
            }
        }

        return changes;
    }

    private static string GetJsonValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "-";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "-",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
    }
}
