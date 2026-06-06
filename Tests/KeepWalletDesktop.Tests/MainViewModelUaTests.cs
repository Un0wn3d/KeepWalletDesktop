using KeepWalletDesktop.ViewModels;
using System.Reflection;

namespace KeepWalletDesktop.Tests;

public class MainViewModelUaTests
{
    [Fact(DisplayName = "Вхід: без логіну і пароля показується повідомлення валідації")]
    public async Task Vkhid_bez_danykh_pokazuie_povidomlennia_validatsii()
    {
        var vm = new MainViewModel
        {
            ApiLogin = "",
            ApiPassword = ""
        };

        await InvokePrivateAsync(vm, "LoginToApiAsync");

        Assert.Equal(vm.Texts.LoginPasswordRequired, vm.ApiStatusMessage);
        Assert.Equal("Auth", vm.CurrentPage);
        Assert.False(vm.IsApiBusy);
    }

    [Fact(DisplayName = "Реєстрація: без обов'язкових полів показується повідомлення валідації")]
    public async Task Reiestratsiia_bez_polei_pokazuie_povidomlennia_validatsii()
    {
        var vm = new MainViewModel
        {
            RegisterUsername = "",
            RegisterEmail = "",
            RegisterPassword = ""
        };

        await InvokePrivateAsync(vm, "RegisterInApiAsync");

        Assert.Equal(vm.Texts.RegisterRequired, vm.ApiStatusMessage);
        Assert.Equal("Auth", vm.CurrentPage);
        Assert.False(vm.IsApiBusy);
    }

    [Fact(DisplayName = "Авторизація: перемикання режиму очищає статус і змінює тексти форми")]
    public void Avtoryzatsiia_peremykannia_rezhymu_onovliuie_formu()
    {
        var vm = new MainViewModel
        {
            ApiStatusMessage = "Попереднє повідомлення"
        };

        Assert.True(vm.IsLoginMode);
        Assert.Equal(vm.Texts.LoginTitle, vm.AuthCardTitle);

        InvokePrivate(vm, "ToggleAuthMode");

        Assert.True(vm.IsRegisterMode);
        Assert.False(vm.IsLoginMode);
        Assert.Equal(string.Empty, vm.ApiStatusMessage);
        Assert.Equal(vm.Texts.RegisterTitle, vm.AuthCardTitle);
        Assert.Equal(vm.Texts.SwitchToLoginLink, vm.AuthFooterAction);
    }

    [Fact(DisplayName = "Налаштування API: порожній хост не зберігається")]
    public void Nalashtuvannia_api_porozhnii_host_pokazuie_validatsiiu()
    {
        var vm = new MainViewModel
        {
            ServerHost = "   ",
            ServerPort = "5000",
            IsServerDialogOpen = true,
            ApiStatusMessage = string.Empty
        };
        var previousBaseUrl = vm.ApiBaseUrl;

        InvokePrivate(vm, "SaveServerSettings");

        Assert.Equal(vm.Texts.ApiBaseUrlMissing, vm.ApiStatusMessage);
        Assert.True(vm.IsServerDialogOpen);
        Assert.Equal(previousBaseUrl, vm.ApiBaseUrl);
    }

    [Fact(DisplayName = "Транзакції: тип дохід перемикає категорії і стан кнопок")]
    public void Tranzaktsii_dokhid_peremykaie_kategorii_ta_stan()
    {
        var vm = new MainViewModel();

        Assert.True(vm.IsExpenseTransactionTypeSelected);
        Assert.Contains(vm.TransactionCategoryOptions, category => category.Code == "groceries");

        vm.TransactionType = "income";

        Assert.True(vm.IsIncomeTransactionTypeSelected);
        Assert.False(vm.IsExpenseTransactionTypeSelected);
        Assert.Equal(vm.Texts.TransactionIncomeType, vm.TransactionTypeDisplay);
        Assert.Contains(vm.TransactionCategoryOptions, category => category.Code == "salary");
        Assert.DoesNotContain(vm.TransactionCategoryOptions, category => category.Code == "groceries");
    }

    private static async Task InvokePrivateAsync(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Метод {methodName} не знайдено.");

        var task = (Task?)method.Invoke(target, null)
            ?? throw new InvalidOperationException($"Метод {methodName} не повернув Task.");

        await task;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Метод {methodName} не знайдено.");

        method.Invoke(target, null);
    }
}
