@ECHO Running Unit Tests

set msbuildpath=%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe
%msbuildpath% cde.sln
..\lib\NUnit-2.5.10..11092\bin\net-2.0\nunit-color-console.exe .\cdeLibTest\bin\Debug\cdelibtest.dll /nologo
@PAUSE