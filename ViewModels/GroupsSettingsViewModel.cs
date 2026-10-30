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
            var selectedGroupId = SelectedAccessGroup?.Id;
            var apiGroups = await _apiClient!.GetGroupsAsync();
            Groups = new ObservableCollection<GroupItemViewModel>(
                apiGroups
                    .Select(g => new GroupItemViewModel(
                        g.Id,
                        g.Name,
                        g.IconKey,
                        g.Color,
                        g.RoleName,
                        g.MemberCount,
                        g.CreatedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture),
                        GetGroupRoleDisplayName(g.RoleName),
                        g.OwnerDisplay))
                    .OrderBy(g => g.RoleSortOrder)
                    .ThenBy(g => g.Name));
            SelectedAccessGroup = Groups.FirstOrDefault(g => g.Id == selectedGroupId) ?? Groups.FirstOrDefault();
            RebuildOpenResourceShareGroups();
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
        foreach (var group in Groups)
        {
            group.IsActive = value is not null && group.Id == value.Id;
        }

        SelectedGroupNameInput = value?.Name ?? string.Empty;
        IsEditingSelectedGroupName = false;
        _ = LoadSelectedGroupMembersAsync();
        OnPropertyChanged(nameof(SelectedGroupBankAccounts));
        OnPropertyChanged(nameof(SelectedGroupScheduledPayments));
        OnPropertyChanged(nameof(SelectedGroupTransactions));
        OnPropertyChanged(nameof(SelectedGroupSavings));
        RefreshGroupAccessProperties();
        OnPropertyChanged(nameof(CanDeleteSelectedGroup));
        OnPropertyChanged(nameof(CanLeaveSelectedGroup));
        OnPropertyChanged(nameof(CanManageSelectedGroupMembers));
    }

    private void RebuildOpenResourceShareGroups()
    {
        if (IsBankAccountDialogOpen)
        {
            var selectedGroupIds = BankAccountShareGroups.Where(g => g.IsSelected).Select(g => g.Id).ToArray();
            BankAccountShareGroups = BuildGroupSelections(selectedGroupIds);
        }

        if (IsTransactionDialogOpen)
        {
            var selectedGroupIds = TransactionShareGroups.Where(g => g.IsSelected).Select(g => g.Id).ToArray();
            TransactionShareGroups = BuildGroupSelections(selectedGroupIds);
        }
    }

    partial void OnSelectedGroupAccessResourceTypeChanged(CategoryOptionViewModel? value)
    {
        OnPropertyChanged(nameof(GroupAccessResourceOptions));
        OnPropertyChanged(nameof(HasGroupAccessResourceOptions));
        SelectedGroupAccessResource = GroupAccessResourceOptions.FirstOrDefault();
    }

    private void RefreshGroupAccessProperties()
    {
        OnPropertyChanged(nameof(HasSelectedGroupBankAccounts));
        OnPropertyChanged(nameof(HasSelectedGroupScheduledPayments));
        OnPropertyChanged(nameof(HasSelectedGroupTransactions));
        OnPropertyChanged(nameof(HasSelectedGroupSavings));
        OnPropertyChanged(nameof(HasSelectedGroupAccessContent));
        OnPropertyChanged(nameof(GroupAccessResourceOptions));
        OnPropertyChanged(nameof(HasGroupAccessResourceOptions));

        if (SelectedGroupAccessResource is null ||
            GroupAccessResourceOptions.All(x => x.ResourceType != SelectedGroupAccessResource.ResourceType || x.Id != SelectedGroupAccessResource.Id))
        {
            SelectedGroupAccessResource = GroupAccessResourceOptions.FirstOrDefault();
        }
    }

    public bool CanDeleteSelectedGroup => SelectedAccessGroup?.IsOwnerRole == true;
    public bool CanLeaveSelectedGroup => SelectedAccessGroup is not null && !SelectedAccessGroup.IsOwnerRole;
    public bool CanManageSelectedGroupMembers => SelectedAccessGroup?.IsOwnerRole == true;

    private string GetGroupRoleDisplayName(string roleName) => roleName.ToLowerInvariant() switch
    {
        "owner" => Texts.GroupRoleOwner,
        "member" => Texts.GroupRoleMember,
        "viewer" => Texts.GroupRoleViewer,
        _ => roleName
    };

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
            var canManageMembers = SelectedAccessGroup.IsOwnerRole;
            SelectedGroupMembers = new ObservableCollection<GroupMemberItemViewModel>(
                members.Select(m => new GroupMemberItemViewModel(
                    m.GroupId,
                    m.UserId,
                    m.Username,
                    m.FullName ?? "-",
                    m.RoleName,
                    m.JoinedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture),
                    canManageMembers && !string.Equals(m.RoleName, "owner", StringComparison.OrdinalIgnoreCase),
                    GetGroupRoleDisplayName(m.RoleName))));
            await LoadGroupUserOptionsAsync();
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

    private async Task LoadGroupUserOptionsAsync()
    {
        try
        {
            EnsureApiClient();
            var users = await _apiClient!.SearchUsersAsync(string.Empty);
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
        if (IsApiBusy || SelectedAccessGroup is null || SelectedGroupUser is null)
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
                new AddGroupMemberRequestDto(SelectedGroupUser.Email, SelectedGroupMemberRole));
            GroupMemberLoginInput = string.Empty;
            SelectedGroupUser = null;
            GroupUserSearchResults = [];
            await LoadGroupsAsync();
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
    private async Task UpdateGroupMemberRoleAsync(GroupMemberItemViewModel? member)
    {
        if (IsApiBusy || SelectedAccessGroup is null || member is null || string.Equals(member.RoleName, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var nextRole = string.Equals(member.RoleName, "viewer", StringComparison.OrdinalIgnoreCase) ? "member" : "viewer";
            await _apiClient!.UpdateGroupMemberRoleAsync(
                SelectedAccessGroup.Id,
                member.UserId,
                new UpdateGroupMemberRoleRequestDto(nextRole));
            await LoadGroupsAsync();
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
    private void EditGroupMemberRole(GroupMemberItemViewModel? member)
    {
        if (member is null || string.Equals(member.RoleName, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        member.SelectedRoleName = member.RoleName;
        member.IsEditingRole = true;
    }

    [RelayCommand]
    private async Task ApplyGroupMemberRoleAsync(GroupMemberItemViewModel? member)
    {
        if (IsApiBusy || SelectedAccessGroup is null || member is null)
        {
            return;
        }

        if (string.Equals(member.SelectedRoleName, member.RoleName, StringComparison.OrdinalIgnoreCase))
        {
            member.IsEditingRole = false;
            return;
        }

        try
        {
            EnsureApiClient();
            if (string.Equals(member.SelectedRoleName, "owner", StringComparison.OrdinalIgnoreCase))
            {
                BeginTransferGroupOwnerConfirmation(member);
                return;
            }

            IsApiBusy = true;
            await _apiClient!.UpdateGroupMemberRoleAsync(
                SelectedAccessGroup.Id,
                member.UserId,
                new UpdateGroupMemberRoleRequestDto(member.SelectedRoleName));
            await LoadGroupsAsync();
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
    private async Task TransferGroupOwnerAsync(GroupMemberItemViewModel? member)
    {
        if (IsApiBusy || SelectedAccessGroup is null || member is null || string.Equals(member.RoleName, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.TransferGroupOwnerAsync(
                SelectedAccessGroup.Id,
                new TransferGroupOwnerRequestDto(member.UserId));
            await LoadGroupsAsync();
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
    private void BeginLeaveSelectedGroup()
    {
        if (SelectedAccessGroup is null || SelectedAccessGroup.IsOwnerRole)
        {
            return;
        }

        PendingLeaveGroup = SelectedAccessGroup;
        PendingGroupOwnerTransferMember = null;
        GroupActionPromptTitle = Texts.LeaveGroupPromptTitle;
        GroupActionPromptMessage = Texts.LeaveGroupPromptMessage;
        IsGroupActionDanger = true;
        IsGroupActionPromptOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmGroupActionAsync()
    {
        if (IsApiBusy || SelectedAccessGroup is null)
        {
            return;
        }

        if (PendingGroupOwnerTransferMember is not null)
        {
            await ConfirmTransferGroupOwnerAsync(PendingGroupOwnerTransferMember);
            return;
        }

        if (PendingLeaveGroup is not null)
        {
            await ConfirmLeaveGroupAsync(PendingLeaveGroup);
        }
    }

    [RelayCommand]
    private void CancelGroupAction()
    {
        if (PendingGroupOwnerTransferMember is not null)
        {
            PendingGroupOwnerTransferMember.SelectedRoleName = PendingGroupOwnerTransferMember.RoleName;
            PendingGroupOwnerTransferMember.IsEditingRole = false;
        }

        CloseGroupActionPrompt();
    }

    private void BeginTransferGroupOwnerConfirmation(GroupMemberItemViewModel member)
    {
        PendingGroupOwnerTransferMember = member;
        PendingLeaveGroup = null;
        GroupActionPromptTitle = Texts.TransferOwnerPromptTitle;
        GroupActionPromptMessage = Texts.TransferOwnerPromptMessage;
        IsGroupActionDanger = false;
        IsGroupActionPromptOpen = true;
    }

    private async Task ConfirmTransferGroupOwnerAsync(GroupMemberItemViewModel member)
    {
        if (SelectedAccessGroup is null)
        {
            CloseGroupActionPrompt();
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.TransferGroupOwnerAsync(
                SelectedAccessGroup.Id,
                new TransferGroupOwnerRequestDto(member.UserId));
            CloseGroupActionPrompt();
            await LoadGroupsAsync();
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

    private async Task ConfirmLeaveGroupAsync(GroupItemViewModel group)
    {
        if (!CurrentUserId.HasValue)
        {
            CloseGroupActionPrompt();
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.RemoveGroupMemberAsync(group.Id, CurrentUserId.Value);
            CloseGroupActionPrompt();
            SelectedAccessGroup = null;
            SelectedGroupMembers = [];
            await LoadBankAccountsAsync();
            await LoadSavingsAsync();
            await LoadScheduledPaymentsAsync();
            await LoadTransactionsAsync();
            await LoadGroupsAsync();
            SelectedAccessGroup ??= Groups.FirstOrDefault();
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

    private void CloseGroupActionPrompt()
    {
        IsGroupActionPromptOpen = false;
        PendingGroupOwnerTransferMember = null;
        PendingLeaveGroup = null;
        GroupActionPromptTitle = string.Empty;
        GroupActionPromptMessage = string.Empty;
        IsGroupActionDanger = false;
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
    private async Task GrantSelectedGroupAccessAsync()
    {
        if (IsApiBusy || SelectedAccessGroup is null || SelectedGroupAccessResource is null)
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();

            switch (SelectedGroupAccessResource.Source)
            {
                case BankAccountItemViewModel account:
                    await _apiClient!.ShareBankAccountWithGroupAsync(
                        account.Id,
                        new ShareResourceWithGroupRequestDto(SelectedAccessGroup.Id));
                    await LoadBankAccountsAsync();
                    await LoadTransactionsAsync();
                    await LoadScheduledPaymentsAsync();
                    break;

                case SavingOptionViewModel saving:
                    await _apiClient!.ShareSavingWithGroupAsync(
                        saving.Id,
                        new ShareResourceWithGroupRequestDto(SelectedAccessGroup.Id));
                    await LoadSavingsAsync();
                    break;

                case ScheduledPaymentItemViewModel payment:
                    await _apiClient!.UpdatePlannedTransactionAsync(
                        payment.Id,
                        new CreatePlannedTransactionRequestDto(
                            payment.Name,
                            payment.AccountId,
                            SelectedAccessGroup.Id,
                            payment.CategoryId,
                            payment.Amount,
                            string.IsNullOrWhiteSpace(payment.Description) ? null : payment.Description,
                            DateOnly.FromDateTime(payment.DueDate.ToLocalTime().Date),
                            payment.RepeatInterval));
                    await LoadScheduledPaymentsAsync();
                    break;
            }

            RefreshGroupAccessProperties();
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
            var createdGroup = await _apiClient!.CreateGroupAsync(new CreateGroupRequestDto(NewGroupNameInput.Trim(), NewGroupIconKey, NewGroupColor));
            NewGroupNameInput = string.Empty;
            NewGroupIconKey = "other";
            NewGroupColor = "#3B82F6";
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

    [RelayCommand]
    private async Task UpdateGroupAsync()
    {
        if (IsApiBusy || SelectedAccessGroup is null || string.IsNullOrWhiteSpace(SelectedGroupNameInput))
        {
            ApiStatusMessage = Texts.TransactionFormValidationText;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var updatedGroup = await _apiClient!.UpdateGroupAsync(
                SelectedAccessGroup.Id,
                new UpdateGroupRequestDto(SelectedGroupNameInput.Trim(), SelectedAccessGroup.SelectedIconKey, SelectedAccessGroup.SelectedColor));
            await LoadGroupsAsync();
            SelectedAccessGroup = Groups.FirstOrDefault(g => g.Id == updatedGroup.Id) ?? SelectedAccessGroup;
            IsEditingSelectedGroupName = false;
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
    private void SelectGroup(GroupItemViewModel? group)
    {
        if (group is not null)
        {
            SelectedAccessGroup = group;
        }
    }

    [RelayCommand]
    private void EditSelectedGroupName()
    {
        if (SelectedAccessGroup is not null)
        {
            IsEditingSelectedGroupName = true;
        }
    }

    [RelayCommand]
    private async Task FinishSelectedGroupNameEditAsync()
    {
        if (!IsEditingSelectedGroupName)
        {
            return;
        }

        if (SelectedAccessGroup is null || string.Equals(SelectedGroupNameInput.Trim(), SelectedAccessGroup.Name, StringComparison.Ordinal))
        {
            IsEditingSelectedGroupName = false;
            return;
        }

        await UpdateGroupAsync();
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(GroupItemViewModel? group)
    {
        group ??= SelectedAccessGroup;
        if (IsApiBusy || group is null || !string.Equals(group.RoleName, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            await _apiClient!.DeleteGroupAsync(group.Id);
            if (SelectedAccessGroup?.Id == group.Id)
            {
                SelectedAccessGroup = null;
                SelectedGroupMembers = [];
            }

            await LoadGroupsAsync();
            SelectedAccessGroup ??= Groups.FirstOrDefault();
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

        await LoadAppSettingsAsync();
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

    [RelayCommand]
    private async Task ToggleRegistrationEnabledAsync()
    {
        if (!IsCurrentUserAdmin || IsApiBusy)
        {
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            var settings = await _apiClient!.UpdateAppSettingsAsync(new UpdateAppSettingsRequestDto(IsRegistrationEnabled));
            IsRegistrationEnabled = settings.RegistrationEnabled;
            ApiStatusMessage = string.Empty;
        }
        catch (HttpRequestException ex)
        {
            IsRegistrationEnabled = !IsRegistrationEnabled;
            ApiStatusMessage = GetApiErrorMessage(ex);
        }
        catch (Exception ex)
        {
            IsRegistrationEnabled = !IsRegistrationEnabled;
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
        finally
        {
            IsApiBusy = false;
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

    [RelayCommand]
    private async Task SaveAdminUserAsync(UserListItemViewModel? user)
    {
        if (user is null || IsApiBusy || !IsCurrentUserAdmin)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(user.UsernameInput) ||
            string.IsNullOrWhiteSpace(user.EmailInput) ||
            string.IsNullOrWhiteSpace(user.SelectedRoleName) ||
            (user.IsNew && string.IsNullOrWhiteSpace(user.PasswordInput)))
        {
            ApiStatusMessage = Texts.RegisterRequired;
            return;
        }

        try
        {
            IsApiBusy = true;
            EnsureApiClient();
            if (user.IsNew)
            {
                await _apiClient!.CreateUserAsync(new CreateUserRequestDto(
                    user.SelectedRoleName,
                    user.UsernameInput.Trim(),
                    user.EmailInput.Trim(),
                    user.PasswordInput,
                    user.FullNameInput.Trim()));
            }
            else
            {
                await _apiClient!.UpdateUserAsync(user.Id, new UpdateUserRequestDto(
                    Role: user.SelectedRoleName,
                    Username: user.UsernameInput.Trim(),
                    Email: user.EmailInput.Trim(),
                    Password: string.IsNullOrWhiteSpace(user.PasswordInput) ? null : user.PasswordInput,
                    FullName: user.FullNameInput.Trim(),
                    IsActive: user.IsActiveInput));
            }
            user.PasswordInput = string.Empty;
            user.IsEditing = false;
            OnPropertyChanged(nameof(AdminUsersEditingAny));
            await LoadAdminUsersAsync();
            ApiStatusMessage = string.Empty;
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
    private void BeginEditAdminUser(UserListItemViewModel? user)
    {
        if (user is null || !IsCurrentUserAdmin)
        {
            return;
        }

        user.IsEditing = true;
        OnPropertyChanged(nameof(AdminUsersEditingAny));
    }

    [RelayCommand]
    private void BeginCreateAdminUser()
    {
        if (!IsCurrentUserAdmin || AdminUsers.Any(user => user.IsNew))
        {
            return;
        }

        var user = new UserListItemViewModel(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "user",
            true,
            DateTimeOffset.Now.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))
        {
            IsEditing = true
        };

        AdminUsers.Insert(0, user);
        OnPropertyChanged(nameof(AdminUsersCountDisplay));
        OnPropertyChanged(nameof(AdminUsersEditingAny));
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
                    l.Username ?? "System",
                    GetActionDisplayName(l.Action, Texts),
                    l.EntityType ?? "-",
                    FormatAuditLog(l, Texts),
                    l.Device ?? "-",
                    l.CreatedAt.ToLocalTime(),
                    l.CreatedAt.ToLocalTime().ToString(DateTimeDisplayFormat, CultureInfo.InvariantCulture))));
            RefreshLogFilters();
        }
        catch (Exception ex)
        {
            ApiStatusMessage = $"{Texts.RequestErrorPrefix}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenAllLogsAsync()
    {
        if (!EnsureAuthorizedNavigation())
        {
            return;
        }

        CurrentPage = LogsPage;
        await LoadUserLogsAsync();
    }

    [RelayCommand]
    private void ClearLogFilters()
    {
        SelectedLogUserFilter = Texts.LogsAllUsersFilter;
        SelectedLogActionFilter = Texts.LogsAllActionsFilter;
        LogDateFromFilter = null;
        LogDateToFilter = null;
        RefreshPagedLogResults(resetVisibleCount: true);
    }

    [RelayCommand]
    private void LoadMoreLogs()
    {
        var nextLogs = FilteredUserLogs
            .Skip(PagedFilteredUserLogs.Count)
            .Take(25)
            .ToList();

        if (nextLogs.Count == 0)
        {
            return;
        }

        foreach (var log in nextLogs)
        {
            PagedFilteredUserLogs.Add(log);
        }

        VisibleLogCount = PagedFilteredUserLogs.Count;
    }

    private void RefreshLogFilters()
    {
        if (string.IsNullOrWhiteSpace(SelectedLogUserFilter))
        {
            SelectedLogUserFilter = Texts.LogsAllUsersFilter;
        }

        if (string.IsNullOrWhiteSpace(SelectedLogActionFilter))
        {
            SelectedLogActionFilter = Texts.LogsAllActionsFilter;
        }

        OnPropertyChanged(nameof(VisibleUserLogs));
        OnPropertyChanged(nameof(LogUserFilterOptions));
        OnPropertyChanged(nameof(LogActionFilterOptions));
        RefreshPagedLogResults(resetVisibleCount: true);
    }

    private void RefreshPagedLogResults(bool resetVisibleCount)
    {
        if (resetVisibleCount)
        {
            VisibleLogCount = 25;
        }

        OnPropertyChanged(nameof(FilteredUserLogs));
        RebuildPagedLogResults();
    }

    private void RebuildPagedLogResults()
    {
        PagedFilteredUserLogs.Clear();
        foreach (var log in FilteredUserLogs.Take(VisibleLogCount))
        {
            PagedFilteredUserLogs.Add(log);
        }

        VisibleLogCount = PagedFilteredUserLogs.Count;
    }

    public void ApplyLogsDateRange(DateTimeOffset from, DateTimeOffset to)
    {
        LogDateFromFilter = from.Date;
        LogDateToFilter = to.Date;
    }

    private static string FormatAuditLog(ApiLogDto log, ILocalizedText texts)
    {
        if (string.IsNullOrWhiteSpace(log.Details))
        {
            return GetActionDisplayName(log.Action, texts);
        }

        try
        {
            using var document = JsonDocument.Parse(log.Details);
            var root = document.RootElement;
            var entityType = GetLogEntityType(log);
            if (string.Equals(entityType, "transactions", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(log.Action, "UPDATE_TRANSACTIONS", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("old_data", out var oldData) &&
                root.TryGetProperty("new_data", out var newData))
            {
                var transactionName = GetLogRowDisplayName(entityType, newData);
                var changes = BuildChangedFields(oldData, newData, new Dictionary<string, string>
                {
                    ["category_name"] = texts.LogFieldCategory,
                    ["amount"] = texts.LogFieldPrice,
                    ["transaction_date"] = texts.LogFieldDate,
                    ["description"] = texts.LogFieldDescription,
                    ["name"] = texts.LogFieldName,
                    ["account_name"] = texts.LogFieldAccount
                });

                return changes.Count == 0
                    ? string.Format(CultureInfo.CurrentCulture, texts.LogDescriptionUpdated, texts.LogEntityTransaction, transactionName)
                    : string.Format(CultureInfo.CurrentCulture, texts.LogDescriptionUpdatedWithChanges, texts.LogEntityTransaction, transactionName, string.Join("; ", changes.Select(c => $"{c.Field}: {c.OldValue} -> {c.NewValue}")));
            }

            if (string.Equals(log.Action, "LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                return texts.LogDescriptionLogin;
            }

            if (string.Equals(log.Action, "LOGOUT", StringComparison.OrdinalIgnoreCase))
            {
                return texts.LogDescriptionLogout;
            }

            if (root.TryGetProperty("new_data", out var createdData))
            {
                return string.Format(CultureInfo.CurrentCulture, texts.LogDescriptionCreated, GetEntityDisplayName(entityType, texts), GetLogRowDisplayName(entityType, createdData));
            }

            if (root.TryGetProperty("deleted_data", out var deletedData))
            {
                return string.Format(CultureInfo.CurrentCulture, texts.LogDescriptionDeleted, GetEntityDisplayName(entityType, texts), GetLogRowDisplayName(entityType, deletedData));
            }
        }
        catch (JsonException)
        {
            return log.Details ?? string.Empty;
        }

        return GetActionDisplayName(log.Action, texts);
    }

    private static string GetLogEntityType(ApiLogDto log)
    {
        if (!string.IsNullOrWhiteSpace(log.EntityType))
        {
            return log.EntityType;
        }

        var action = log.Action.ToUpperInvariant();
        return action switch
        {
            "LOGIN" or "LOGOUT" => "refresh_tokens",
            _ when action.StartsWith("CREATE_", StringComparison.Ordinal) => action["CREATE_".Length..].ToLowerInvariant(),
            _ when action.StartsWith("UPDATE_", StringComparison.Ordinal) => action["UPDATE_".Length..].ToLowerInvariant(),
            _ when action.StartsWith("DELETE_", StringComparison.Ordinal) => action["DELETE_".Length..].ToLowerInvariant(),
            _ => string.Empty
        };
    }

    private static string GetLogRowDisplayName(string? entityType, JsonElement data)
    {
        var preferredFields = (entityType ?? string.Empty).ToLowerInvariant() switch
        {
            "users" => new[] { "username", "full_name", "email", "id" },
            "groups" => new[] { "group_name", "name", "id" },
            "group_members" => new[] { "username", "group_name", "user_id", "group_id" },
            "accounts" => new[] { "account_name", "name", "id" },
            "budgets" => new[] { "account_name", "category_name", "id" },
            "categories" => new[] { "category_name", "name", "id" },
            "transactions" => new[] { "transaction_name", "name", "account_name", "id" },
            "savings" => new[] { "saving_name", "name", "id" },
            "wish_list" => new[] { "name", "saving_name", "id" },
            "user_category_preferences" => new[] { "username", "category_name", "user_id" },
            "group_resource_access" => new[] { "group_name", "account_name", "saving_name", "transaction_name", "group_id" },
            "recurring_payments" => new[] { "recurring_payment_name", "name", "id" },
            "refresh_tokens" => new[] { "username", "user_id" },
            _ => new[] { "name", "username", "account_name", "group_name", "saving_name", "transaction_name", "id" }
        };

        foreach (var field in preferredFields)
        {
            var value = GetJsonValue(data, field);
            if (!string.IsNullOrWhiteSpace(value) && value != "-")
            {
                return value;
            }
        }

        return "-";
    }

    private static string GetActionDisplayName(string action, ILocalizedText texts)
    {
        return action.ToUpperInvariant() switch
        {
            "CREATE_USERS" => texts.LogActionCreateUsers,
            "UPDATE_USERS" => texts.LogActionUpdateUsers,
            "DELETE_USERS" => texts.LogActionDeleteUsers,
            "CREATE_GROUPS" => texts.LogActionCreateGroups,
            "UPDATE_GROUPS" => texts.LogActionUpdateGroups,
            "DELETE_GROUPS" => texts.LogActionDeleteGroups,
            "CREATE_GROUP_MEMBERS" => texts.LogActionCreateGroupMembers,
            "UPDATE_GROUP_MEMBERS" => texts.LogActionUpdateGroupMembers,
            "DELETE_GROUP_MEMBERS" => texts.LogActionDeleteGroupMembers,
            "CREATE_ACCOUNTS" => texts.LogActionCreateAccounts,
            "UPDATE_ACCOUNTS" => texts.LogActionUpdateAccounts,
            "DELETE_ACCOUNTS" => texts.LogActionDeleteAccounts,
            "CREATE_TRANSACTIONS" => texts.LogActionCreateTransactions,
            "UPDATE_TRANSACTIONS" => texts.LogActionUpdateTransactions,
            "DELETE_TRANSACTIONS" => texts.LogActionDeleteTransactions,
            "CREATE_SAVINGS" => texts.LogActionCreateSavings,
            "UPDATE_SAVINGS" => texts.LogActionUpdateSavings,
            "DELETE_SAVINGS" => texts.LogActionDeleteSavings,
            "CREATE_BUDGETS" => texts.LogActionCreateBudgets,
            "UPDATE_BUDGETS" => texts.LogActionUpdateBudgets,
            "DELETE_BUDGETS" => texts.LogActionDeleteBudgets,
            "CREATE_CATEGORIES" => texts.LogActionCreateCategories,
            "UPDATE_CATEGORIES" => texts.LogActionUpdateCategories,
            "DELETE_CATEGORIES" => texts.LogActionDeleteCategories,
            "CREATE_RECURRING_PAYMENTS" => texts.LogActionCreateRecurringPayments,
            "UPDATE_RECURRING_PAYMENTS" => texts.LogActionUpdateRecurringPayments,
            "DELETE_RECURRING_PAYMENTS" => texts.LogActionDeleteRecurringPayments,
            "CREATE_WISH_LIST" => texts.LogActionCreateWishList,
            "UPDATE_WISH_LIST" => texts.LogActionUpdateWishList,
            "DELETE_WISH_LIST" => texts.LogActionDeleteWishList,
            "CREATE_USER_CATEGORY_PREFERENCES" => texts.LogActionCreateUserCategoryPreferences,
            "UPDATE_USER_CATEGORY_PREFERENCES" => texts.LogActionUpdateUserCategoryPreferences,
            "DELETE_USER_CATEGORY_PREFERENCES" => texts.LogActionDeleteUserCategoryPreferences,
            "CREATE_REFRESH_TOKENS" or "LOGIN" => texts.LogActionLogin,
            "DELETE_REFRESH_TOKENS" or "LOGOUT" => texts.LogActionLogout,
            _ => action.Replace('_', ' ').ToLowerInvariant()
        };
    }

    private static string GetEntityDisplayName(string? entityType, ILocalizedText texts)
    {
        return (entityType ?? string.Empty).ToLowerInvariant() switch
        {
            "users" => texts.LogEntityUser,
            "groups" => texts.LogEntityGroup,
            "group_members" => texts.LogEntityGroupMember,
            "accounts" => texts.LogEntityAccount,
            "transactions" => texts.LogEntityTransaction,
            "savings" => texts.LogEntitySaving,
            "budgets" => texts.LogEntityBudget,
            "categories" => texts.LogEntityCategory,
            "recurring_payments" => texts.LogEntityRecurringPayment,
            "wish_list" => texts.LogEntityWishList,
            "user_category_preferences" => texts.LogEntityCategorySettings,
            "refresh_tokens" => texts.LogEntitySession,
            _ => entityType ?? texts.LogEntityRecord
        };
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
