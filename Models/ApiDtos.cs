using System;
using System.Collections.Generic;

namespace KeepWalletDesktop.Models;

public sealed record LoginRequestDto(string Login, string Password);

public sealed record RegisterRequestDto(string Username, string Email, string Password, string? FullName);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    string Username,
    string Email,
    string? RoleName);

public sealed record RefreshResponseDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record ApiUserDto(
    Guid Id,
    string Username,
    string Email,
    string? FullName,
    bool IsActive,
    string? RoleName,
    DateTimeOffset CreatedAt);

public sealed record UpdateUserRequestDto(
    string? Role = null,
    string? Username = null,
    string? Email = null,
    string? Password = null,
    string? FullName = null,
    bool? IsActive = null);

public sealed record CreateGroupRequestDto(string Name);

public sealed record AddGroupMemberRequestDto(string LoginOrEmail, string RoleName);

public sealed record ShareResourceWithGroupRequestDto(Guid? GroupId);

public sealed record ApiGroupDto(
    Guid Id,
    string Name,
    string RoleName,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record ApiGroupMemberDto(
    Guid GroupId,
    Guid UserId,
    string Username,
    string? FullName,
    string RoleName,
    DateTimeOffset JoinedAt);

public sealed record ApiLogDto(
    long Id,
    Guid? UserId,
    string? Username,
    string Action,
    string? EntityType,
    string? Details,
    string? Device,
    DateTimeOffset CreatedAt);

public sealed record CreateTransactionRequestDto(
    Guid AccountId,
    int CategoryId,
    int? RecurringPaymentId,
    decimal Amount,
    string? Description,
    DateTimeOffset TransactionDate);

public sealed record UpdateTransactionRequestDto(
    Guid AccountId,
    int CategoryId,
    int? RecurringPaymentId,
    decimal Amount,
    string? Description,
    DateTimeOffset TransactionDate);

public sealed record ApiTransactionDto(
    int Id,
    Guid AccountId,
    int CategoryId,
    int? RecurringPaymentId,
    decimal Amount,
    string? Description,
    DateTimeOffset TransactionDate);

public sealed record ApiCategoryDto(
    int Id,
    string Name,
    string Type,
    string IconKey);

public sealed record ApiBudgetDto(
    int Id,
    Guid UserId,
    Guid? GroupId,
    int CategoryId,
    decimal Amount,
    TimeSpan? BudgetPeriod,
    DateOnly StartDate,
    bool IsActive);

public sealed record UpsertBudgetRequestDto(
    Guid? GroupId,
    decimal Amount,
    TimeSpan? BudgetPeriod,
    DateOnly StartDate,
    bool IsActive);

public sealed record CreateCategoryRequestDto(
    string Name,
    string Type,
    string? IconKey = null);

public sealed record UpdateCategoryRequestDto(
    string? Name = null,
    string? IconKey = null);

public sealed record UserCategoryPreferenceDto(
    int Id,
    string Name,
    string Type,
    string IconKey,
    bool IsSelected);

public sealed record UpdateUserCategoryPreferencesRequestDto(
    IReadOnlyList<int> SelectedCategoryIds);

public sealed record MergeCategoryRequestDto(
    int TargetCategoryId);

public sealed record CreateSavingRequestDto(
    string Name,
    decimal? TargetAmount,
    decimal CurrentAmount,
    DateOnly? Deadline);

public sealed record UpdateSavingRequestDto(
    string Name,
    decimal? TargetAmount,
    decimal CurrentAmount,
    DateOnly? Deadline,
    bool IsCompleted);

public sealed record ApiSavingDto(
    int Id,
    Guid UserId,
    Guid? GroupId,
    string Name,
    decimal? TargetAmount,
    decimal CurrentAmount,
    DateOnly? Deadline,
    bool IsCompleted,
    DateTimeOffset CreatedAt);

public sealed record CreateSavingItemRequestDto(string Name, decimal Price, short? Priority, bool IsPurchased);

public sealed record UpdateSavingItemRequestDto(string Name, decimal Price, short? Priority, bool IsPurchased);

public sealed record ApiSavingItemDto(
    int Id,
    int SavingId,
    string Name,
    decimal Price,
    short? Priority,
    bool IsPurchased);

public sealed record CreatePlannedTransactionRequestDto(
    string Name,
    Guid AccountId,
    int CategoryId,
    decimal Amount,
    string? Description,
    DateOnly NextDueDate,
    TimeSpan RepeatInterval);

public sealed record ApiPlannedTransactionDto(
    int Id,
    Guid AccountId,
    int CategoryId,
    int RecurringPaymentId,
    string Name,
    decimal Amount,
    string? Description,
    DateTimeOffset TransactionDate,
    TimeSpan RepeatInterval,
    DateOnly NextDueDate,
    bool IsActive);

public sealed record CreateBankAccountRequestDto(
    string Name,
    string Currency,
    decimal Balance,
    bool IsDefault);

public sealed record UpdateBankAccountRequestDto(
    string Name,
    string Currency,
    decimal Balance,
    bool IsDefault);

public sealed record ApiBankAccountDto(
    Guid Id,
    Guid UserId,
    Guid? GroupId,
    string Name,
    string Currency,
    decimal Balance,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
