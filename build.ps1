##########################################################################
# This is a Cake bootstrapper script for PowerShell.
# Reference: https://andrewlock.net/simplifying-the-cake-global-tool-bootstrapper-scripts-in-netcore3-with-local-tools/
# Official Reference: https://github.com/cake-build/resources
#
# Not using official as this one leverages .net core 3.0 local tool
# for cake which seems like a good idea.
##########################################################################

[CmdletBinding()]
Param(
    [string]$Script = "build.cake",
    [string]$Target,
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

# Restore Cake tool
& dotnet tool restore

# Build Cake arguments
$cakeArguments = @("$Script");
if ($Target) { $cakeArguments += "--target=$Target" }
$cakeArguments += $ScriptArgs

# Not use this until the module for dotnet tool install is built in i think.
## Reference: https://cakebuild.net/docs/fundamentals/preprocessor-directives#module-directive
## bootstrap modules for cake first, main module of interest at moment is Cake.DotNetTool.Module
#& dotnet tool run dotnet-cake --bootstrap

& dotnet tool run dotnet-cake -- $cakeArguments
exit $LASTEXITCODE
