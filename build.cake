#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#tool "nuget:?package=ILRepack&version=2.0.16"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./dist") + Directory(configuration);
var slnDir = "./src/cde.sln";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore("./src/cde.sln");
});

Task("Package")
.Does(()=> {
  var cdeAssemblyPaths = GetFiles("./src/cde/bin/Release/*.dll");
      ILRepack(
            buildDir + File("Cde.exe"),
            "./src/cde/bin/Release/cde.exe",
            cdeAssemblyPaths
      );

// var cdeWinAssemblyPaths = new FilePathCollection();
//   cdeWinAssemblyPaths.Add(GetFiles("./src/cde/bin/Release/AlphaFS.dll"));
//   cdeWinAssemblyPaths.Add(GetFiles("./src/cde/bin/Release/ndesk.options.dll"));
//   cdeWinAssemblyPaths.Add(GetFiles("./src/cde/bin/Release/protobuf-net.dll"));
//   cdeWinAssemblyPaths.Add(GetFiles("./src/cde/bin/Release/cdelib.dll"));
//   cdeWinAssemblyPaths.Add(GetFiles("./src/cde/bin/Release/Util.dll"));
    //issue with serilog + ilpack?
    var cdeWinAssemblyPaths = GetFiles("./src/cdeWin/bin/Release/*.dll");
      ILRepack(
            buildDir + File("CdeWin.exe"),
            "./src/cdeWin/bin/Release/cdeWin.exe",
            cdeWinAssemblyPaths
      );

});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(slnDir, settings =>
        settings.SetConfiguration(configuration));
  }
    else
    {
      // Use XBuild
      XBuild(slnDir, settings =>
        settings.SetConfiguration(configuration));
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3("./src/**/bin/" + configuration + "/*.Tests.dll", new NUnit3Settings {
        NoResults = true
        });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Package");    

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);