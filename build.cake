#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0"

using Path = System.IO.Path;
using System.Xml;

// Reference: https://github.com/OctopusDeploy/Calamari/blob/master/build.cake
// Used above reference for structure of this build file.

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testFilter = Argument("where", "");
var netcoreAppVersion = Argument("netcoreapp", "3.0");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

// var projects = GetFiles("./**/*.csproj");
// var projectPaths = projects.Select(project => project.GetDirectory().ToString());
// var artifactsDir = "./artifacts";
var publishDir = "./publish";
// var coverageThreshold = 100;

///////////////////////////////////////////////////////////////////////////////
// Git Version
///////////////////////////////////////////////////////////////////////////////

GitVersion gitVersionInfo;
string nugetVersion = "1.0.0";
string informationalVersion = "1.0.0";
bool isMasterBranch = false;

try {
    // Git Version \\(^_^)//
    gitVersionInfo = GitVersion(new GitVersionSettings {
        OutputType = GitVersionOutput.Json
    });

    // Package Version
    nugetVersion = gitVersionInfo.NuGetVersion;
    informationalVersion = gitVersionInfo.InformationalVersion;
    isMasterBranch = gitVersionInfo.BranchName == "master";
}
catch
{
    Error("Could not set GitVersionInfo");
}

///////////////////////////////////////////////////////////////////////////////
// Artifacts
///////////////////////////////////////////////////////////////////////////////

// EnsureDirectoryExists(artifactsDir);
// var artifactsDirectory = Directory(artifactsDir);

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (gitVersionInfo != null)
    {
        Information("Building Version {0} on {1}", nugetVersion, gitVersionInfo.BranchName);
    }
    else
    {
        Information("Building an unknown version");
    }

    // List .NET Core Version
    Information(".NET Core installed");
    StartProcess("dotnet", new ProcessSettings {Arguments = "--info"});
});

Teardown(context =>
{
    if (gitVersionInfo != null)
    {
        Information("Finished building Version {0} on {1}", nugetVersion, gitVersionInfo.BranchName);
    }
    else
    {
        Information("Finished building an unknown version");
    }
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        // CleanDirectories(artifactsDirectory);
        CleanDirectories("./**/obj");
        CleanDirectories("./**/bin");
        CleanDirectories("./test/**/TestResults");
    }
);

Task("Restore")
	.IsDependentOn("Clean")
    .Does(() => DotNetCoreRestore("src", new DotNetCoreRestoreSettings
    {
	    ArgumentCustomization = args => args.Append($"--verbosity normal")
    }));

// Task("Build")
// 	.IsDependentOn("Restore")
//     .Does(() =>
//     {
//         DotNetCoreBuild("./src/cde.sln", new DotNetCoreBuildSettings {
//             Configuration = configuration,
//             ArgumentCustomization = args =>
//                 args.Append($"/p:Version={nugetVersion}")
//                     .Append($"--verbosity normal")
//                     .Append($"/p:InformationalVersion={informationalVersion}")
//                     // .Append($"/p:PublishSingleFile=true")
//                     // .Append($"/p:PublishTrimmed=true") // mutually exclusive with self contained
//                     .Append($"/p:SelfContained=false")
//         });
//     });

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
	{
		DotNetCoreBuild("./src/cde.sln", new DotNetCoreBuildSettings
		{
			Configuration = configuration,
			ArgumentCustomization = args => args
                .Append($"/p:Version={nugetVersion}")
                .Append($"--verbosity normal")
		});
	});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var projects = GetFiles("./src/**/*LibTest.csproj");
		foreach(var project in projects)
			DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
			{
				Configuration = configuration,
				NoBuild = true,
				ArgumentCustomization = args => {
					if(!string.IsNullOrEmpty(testFilter)) {
						args = args.Append("--where").AppendQuoted(testFilter);
					}
					return args
					    .Append("--logger:trx")
                        .Append($"--verbosity normal");
				}
			});        
    });

Task("Pack")
	.IsDependentOn("Build")
    .Does(() =>
{
    // Create the self-contained packages for each runtime ID defined
    foreach(var rid in GetProjectRuntimeIds(@".\src\cde\cde.csproj"))
    {
        DoPackage("cde", "netcoreapp3.0", nugetVersion, rid);
    }
	
	// // Create a Zip for each runtime for testing
	// foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari.Tests\Calamari.Tests.csproj"))
    // {
	// 	var publishedLocation = DoPublish("Calamari.Tests", "netcoreapp2.2", nugetVersion, rid);
	// 	var zipName = $"Calamari.Tests.netcoreapp2.{rid}.{nugetVersion}.zip";
	// 	Zip(Path.Combine(publishedLocation, rid), Path.Combine(artifactsDir, zipName));
    // }
});

private void DoPackage(string project, string framework, string version, string runtimeId = null)
{
    var publishedTo = Path.Combine(publishDir, project, framework);
    var projectDir = Path.Combine("./src", project);
    var packageId = $"{project}";
    var nugetPackProperties = new Dictionary<string,string>();
    var publishSettings = new DotNetCorePublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishedTo,
        Framework = framework,
		ArgumentCustomization = args => args
		    .Append($"/p:Version={nugetVersion}")
		    .Append($"--verbosity normal")
		    .Append($"/p:PublishSingleFile=true") // try it
            .Append($"/p:SelfContained=false") // try it
    };
    if (!string.IsNullOrEmpty(runtimeId))
    {
        publishedTo = Path.Combine(publishedTo, runtimeId);
        publishSettings.OutputDirectory = publishedTo;
        // "portable" is not an actual runtime ID. We're using it to represent the portable .NET core build.
        publishSettings.Runtime = (runtimeId != null && runtimeId != "portable") ? runtimeId : null;
        packageId = $"{project}.{runtimeId}";
        nugetPackProperties.Add("runtimeId", runtimeId);
    }
    // var nugetPackSettings = new NuGetPackSettings
    // {
    //     Id = packageId,
    //     OutputDirectory = artifactsDir,
	// 	BasePath = publishedTo,
	// 	Version = nugetVersion,
	// 	Verbosity = NuGetVerbosity.Normal,
    //     Properties = nugetPackProperties
    // };

    DotNetCorePublish(projectDir, publishSettings);

    // SignAndTimestampBinaries(publishSettings.OutputDirectory.FullPath);
    // var nuspec = $"{publishedTo}/{packageId}.nuspec";
    // CopyFile($"{projectDir}/{project}.nuspec", nuspec);
    // NuGetPack(nuspec, nugetPackSettings);
}

// Returns the runtime identifiers from the project file
private IEnumerable<string> GetProjectRuntimeIds(string projectFile)
{
    var doc = new XmlDocument();
    doc.Load(projectFile);
    var rids = doc.SelectSingleNode("Project/PropertyGroup/RuntimeIdentifiers").InnerText;
    return rids.Split(';');
}

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Test")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);