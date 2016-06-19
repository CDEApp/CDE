mkdir bin\AnyCPU
mkdir bin\x86

del bin\AnyCPU\*.exe
del bin\AnyCPU\*.dll
del bin\AnyCPU\*.pdb
del bin\AnyCPU\*.config

del bin\x86\*.exe
del bin\x86\*.dll
del bin\x86\*.pdb
del bin\x86\*.config

set t=..\..\..\..
REM OLD set ilmerge="c:\Program Files (x86)\Microsoft\ILMerge\ILMerge.exe"
set ilmerge=%t%\lib\ILMerge\ILMerge.exe
REM set msbuildpath=%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe
set msbuildpath="C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe"

goto BUILD86

@ECHO Building Any CPU
cd src
REM manual deletes cause /Rebuild really doesnt work right
del cde\bin\Release\*.exe
del cde\bin\Release\*.dll
del cde\bin\Release\*.config
del cde\bin\Release\*.pdb
del cdeWin\bin\Release\*.exe
del cdeWin\bin\Release\*.dll
del cdeWin\bin\Release\*.pdb
%msbuildpath% /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU" cde.sln
cd ..

@echo Merging Any CPU
set sTbin =bin\AnyCPU
set tbin=%t%\%sTbin%
copy History.txt %sTbin%
cd src\cde\bin\Release
rem %ilmerge% /targetplatform:v4,c:\windows\Microsoft.Net\Framework\v4.0.30319 /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
%ilmerge% /targetplatform:"v4,C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
copy cde.exe.config %tbin%
cd %t%

cd src\cdeWin\bin\Release
rem %ilmerge% /targetplatform:v4,c:\windows\Microsoft.Net\Framework\v4.0.30319 /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll
%ilmerge% /targetplatform:"v4,C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll Util.dll
cd %t%

:BUILD86
@echo Building x86
cd src
REM set msbuildpath=%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe

REM manual deletes cause /Rebuild really doesn't work right
del cde\bin\Release\*.exe
del cde\bin\Release\*.dll
del cde\bin\Release\*.config
del cde\bin\Release\*.pdb
del cdeWin\bin\Release\*.exe
del cdeWin\bin\Release\*.dll
del cdeWin\bin\Release\*.pdb
%msbuildpath% /t:Rebuild /p:Configuration=Release /p:Platform="x86" cde.sln
cd ..
rem goto END

@echo Merging x86
set sTbin=bin\x86
set tbin=%t%\%sTbin%
copy History.txt %sTbin%
cd src\cde\bin\Release
rem %ilmerge% /targetplatform:v4,c:\windows\Microsoft.Net\Framework\v4.0.30319 /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
rem %ilmerge% /targetplatform:"v4,C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
%ilmerge% /targetplatform:"v4,C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
copy cde.exe.config %tbin%

cd %t%

cd src\cdeWin\bin\Release
rem %ilmerge% /targetplatform:v4,c:\windows\Microsoft.Net\Framework\v4.0.30319 /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll
rem %ilmerge% /targetplatform:"v4,C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll
%ilmerge% /targetplatform:"v4,C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0" /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll Util.dll
cd %t%

rem @ECHO Running Unit Tests
rem ..\lib\NUnit-2.5.10..11092\bin\net-2.0\nunit-color-console.exe .\cdeLibTest\bin\Debug\cdelibtest.dll /nologo

:END
@pause
