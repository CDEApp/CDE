@ECHO Build with Cake
dotnet tool install --global Cake.Tool --version 0.35.0
dotnet cake build.cake
REM powershell .\build.ps1