using System.Runtime.CompilerServices;

namespace ReplyInMyVoice.Tests;

internal static class TestHostEnvironment
{
    [ModuleInitializer]
    public static void Configure()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
    }
}
