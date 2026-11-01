using System;
using System.IO;
using cdeLib.Infrastructure.Config;
using NUnit.Framework;

// In the global namespace, so it covers every test in this assembly.

/// <summary>
/// cde writes a default appsettings.json into the user config folder when it finds none (#158). Pointing the config
/// folder at a temporary one for the whole test run keeps a test that starts cde in-process (or as a child process,
/// which inherits the variable) from writing into, or reading from, the real <c>~/.config/cde</c> or <c>%APPDATA%\cde</c>.
/// </summary>
[SetUpFixture]
public class ConfigFolderIsolation
{
    private string _previous;
    private string _folder;

    [OneTimeSetUp]
    public void PointTheConfigFolderAtATemporaryOne()
    {
        _previous = Environment.GetEnvironmentVariable(AppSettingsLocator.ConfigDirVariable);
        _folder = Directory.CreateTempSubdirectory("cde-test-config").FullName;
        Environment.SetEnvironmentVariable(AppSettingsLocator.ConfigDirVariable, _folder);
    }

    [OneTimeTearDown]
    public void Restore()
    {
        Environment.SetEnvironmentVariable(AppSettingsLocator.ConfigDirVariable, _previous);
        try { Directory.Delete(_folder, true); } catch (IOException) { /* ignore */ }
    }
}
