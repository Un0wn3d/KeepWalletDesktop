using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace KeepWalletDesktop.Services;

public static class AppConfiguration
{
    private static readonly Lazy<IConfigurationRoot> ConfigurationRoot = new(CreateConfiguration);

    public static IConfiguration Configuration => ConfigurationRoot.Value;

    public static ApiOptions GetApiOptions()
    {
        return new ApiOptions
        {
            BaseUrl = Configuration["Api:BaseUrl"] ?? "http://localhost:5174"
        };
    }

    private static IConfigurationRoot CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }
}
