using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public Build()
    {
        var msBuildExtensionPath = Environment.GetEnvironmentVariable("MSBuildExtensionsPath");
        var msBuildExePath = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH");
        var msBuildSdkPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");

        MSBuildLocator.RegisterDefaults();
        TriggerAssemblyResolution();

        Environment.SetEnvironmentVariable("MSBuildExtensionsPath", msBuildExtensionPath);
        Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msBuildExePath);
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", msBuildSdkPath);
    }

    static void TriggerAssemblyResolution() => _ = new ProjectCollection();

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";


    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Pack => _ => _
        .After(Compile, Test)
        .DependsOn(Test)
        .Produces(ArtifactsDirectory / "*.*")
        .Executes(() =>
        {
            EnsureCleanDirectory(ArtifactsDirectory);
            DotNetPack(s => s
                .SetProject(Solution)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetIncludeSymbols(true)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
            );
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            DotNetPublish(s => s
                    .SetProject("src/cde/cde.csproj")
                    .SetConfiguration(Configuration)
                    .SetOutput(ArtifactsDirectory + "/cde")
                    .SetRuntime("win10-x64")
                    .SetPublishSingleFile(true)
                    .SetSelfContained(false)
            );

            DotNetPublish(s => s
                    .SetProject("src/cdewin/cdewin.csproj")
                    .SetConfiguration(Configuration)
                    .SetOutput(ArtifactsDirectory + "/cdewin")
                    .SetRuntime("win10-x64")
                    .SetPublishSingleFile(true)
                    .SetSelfContained(false)
            );
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild());
        });
}