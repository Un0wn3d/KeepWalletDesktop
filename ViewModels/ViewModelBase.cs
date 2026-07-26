using CommunityToolkit.Mvvm.ComponentModel;
using KeepWalletDesktop.Services;
using System.Net;
using System.Net.Http;

namespace KeepWalletDesktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected string GetApiErrorMessage(HttpRequestException ex, ILocalizedText texts)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized => texts.UnauthorizedError,
            HttpStatusCode.Conflict => texts.ConflictError,
            HttpStatusCode.BadRequest => $"{texts.ValidationErrorPrefix}: {ex.Message}",
            _ => $"{texts.RequestErrorPrefix}: {ex.Message}"
        };
    }
}
