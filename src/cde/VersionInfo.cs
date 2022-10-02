using System.Reflection;

namespace cde;

public class VersionInfo
{
    public static string GetInformationalVersion() =>
        Assembly
            .GetEntryAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;
}