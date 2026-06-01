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
    }

    [Fact(DisplayName = "Головне меню: транзакція без валідних даних не зберігається")]
    public async Task Holovne_meniu_nevalidna_tranzaktsiia_daie_validatsiiu()
    {
        var vm = new MainViewModel
        {
            TransactionAmountInput = "0",
            SelectedTransactionCategory = null
        };

        await InvokePrivateAsync(vm, "SaveTransactionAsync");

        Assert.Equal(vm.Texts.TransactionFormValidationText, vm.ApiStatusMessage);
    }

    private static async Task InvokePrivateAsync(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Метод {methodName} не знайдено.");

        var task = (Task?)method.Invoke(target, null)
            ?? throw new InvalidOperationException($"Метод {methodName} не повернув Task.");

        await task;
    }
}
