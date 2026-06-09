using System;
using JetBrains.Annotations;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Fallout.Common;
using Fallout.Common.CI;
using Fallout.Common.Git;
using Fallout.Common.IO;
using Fallout.Common.ProjectModel;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Utilities.Collections;
using static Fallout.Common.Tools.DotNet.DotNetTasks;
// ReSharper disable AllUnderscoreLocalParameterName

[ShutdownDotNetAfterServerBuild]
class Build : FalloutBuild
{
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

    AbsolutePath SourceDirectory => RootDirectory / "src";

    AbsolutePath OutputDirectory => RootDirectory / "output";

    AbsolutePath TestsDirectory => RootDirectory / "tests";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    readonly string RunTime = "win-x64";


    [UsedImplicitly]
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x => x.DeleteDirectory());
            OutputDirectory.CreateOrCleanDirectory();
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
            ArtifactsDirectory.CreateOrCleanDirectory();
            DotNetPack(s => s
                .SetProject(Solution)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableIncludeSymbols()
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
            );
        });

    [UsedImplicitly]
    Target Publish => _ => _
        //.DependsOn(Pack)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetProject("src/cde/cde.csproj")
                .SetConfiguration(Configuration)
                .SetOutput($"{ArtifactsDirectory}/cde")
                .SetRuntime(RunTime)
                .EnablePublishSingleFile()
                .DisableSelfContained()
            );

            DotNetPublish(s => s
                .SetProject("src/cdewin/cdewin.csproj")
                .SetConfiguration(Configuration)
                .SetOutput($"{ArtifactsDirectory}/cdewin")
                .SetRuntime(RunTime)
                .EnablePublishSingleFile()
                .DisableSelfContained()
            );

            // cdeApi ships as a self-contained single-file binary so the Tauri frontend can bundle it
            // as a sidecar on machines without the .NET runtime installed.
            DotNetPublish(s => s
                .SetProject("src/cdeApi/cdeApi.csproj")
                .SetConfiguration(Configuration)
                .SetOutput($"{ArtifactsDirectory}/cdeApi")
                .SetRuntime(RunTime)
                .EnablePublishSingleFile()
                .EnableSelfContained()
            );
        });

    [UsedImplicitly]
    Target PublishRelease => _ => _
        .Description("Publish in Release configuration (always)")
        .Executes(() =>
        {
            var releaseConfig = Configuration.Release;

            DotNetPublish(s => s
                .SetProject("src/cde/cde.csproj")
                .SetConfiguration(releaseConfig)
                .SetOutput($"{ArtifactsDirectory}/cde")
                .SetRuntime(RunTime)
                .EnablePublishSingleFile()
                .DisableSelfContained()
            );

            DotNetPublish(s => s
                .SetProject("src/cdewin/cdewin.csproj")
                .SetConfiguration(releaseConfig)
                .SetOutput($"{ArtifactsDirectory}/cdewin")
                .SetRuntime(RunTime)
                .EnablePublishSingleFile()
                .DisableSelfContained()
            );

            DotNetPublish(s => s
                .SetProject("src/cdeApi/cdeApi.csproj")
                .SetConfiguration(releaseConfig)
                .SetOutput($"{ArtifactsDirectory}/cdeApi")
                .SetRuntime(RunTime)
                .EnablePublishSingleFile()
                .EnableSelfContained()
            );
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    [UsedImplicitly]
    Target CompileRelease => _ => _
        .Description("Build in Release configuration (always)")
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration.Release)
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