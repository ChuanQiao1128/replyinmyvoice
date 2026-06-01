using System.Runtime.CompilerServices;

namespace ReplyInMyVoice.Tests;

internal static class TestEnvironmentBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
    }
}
