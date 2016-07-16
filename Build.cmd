mkdir bin\AnyCPU
mkdir bin\x86

@REM 
@REM 
@REM 
@REM To use ilmerge use reference as follows.
@REM 
@REM Reference http://stackoverflow.com/questions/2961357/using-ilmerge-with-net-4-libraries/2962378#2962378
@REM 
@REM 
@REM 

set NET461=V4,C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1

del bin\AnyCPU\*.exe
del bin\AnyCPU\*.dll
del bin\AnyCPU\*.pdb
del bin\AnyCPU\*.config

del bin\x86\*.exe
del bin\x86\*.dll
del bin\x86\*.pdb
del bin\x86\*.config

set t=..\..\..\..
set ilmerge=%t%\src\packages\ILMerge.2.14.1208\tools\ilmerge.exe
set msbuildpath="C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe"
@REM goto BUILD86

@ECHO Building Any CPU
type .\lib\text\build_any.txt
cd src
@REM manual deletes cause /Rebuild really doesnt work right
del cde\bin\Release\*.exe
del cde\bin\Release\*.dll
del cde\bin\Release\*.config
del cde\bin\Release\*.pdb
del cdeWin\bin\Release\*.exe
del cdeWin\bin\Release\*.dll
del cdeWin\bin\Release\*.pdb
%msbuildpath% /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU" cde.sln
cd ..
@ECHO.
@ECHO.
@ECHO -------------------------
@echo Merging Any CPU
@ECHO -------------------------
@ECHO.
@ECHO.
set sTbin=bin\AnyCPU
set tbin=%t%\%sTbin%
copy History.txt %sTbin%
cd src\cde\bin\Release
%ilmerge% /targetplatform:"%NET461%" /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll

copy cde.exe.config %tbin%
cd %t%

cd src\cdeWin\bin\Release
%ilmerge% /targetplatform:"%NET461%" /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll Util.dll

cd %t%

:BUILD86
@echo Building x86
type .\lib\text\build_x86.txt
cd src

@REM manual deletes cause /Rebuild really doesn't work right
del cde\bin\Release\*.exe
del cde\bin\Release\*.dll
del cde\bin\Release\*.config
del cde\bin\Release\*.pdb
del cdeWin\bin\Release\*.exe
del cdeWin\bin\Release\*.dll
del cdeWin\bin\Release\*.pdb
%msbuildpath% /t:Rebuild /p:Configuration=Release /p:Platform="x86" cde.sln
cd ..

@ECHO.
@ECHO.
@ECHO -------------------------
@echo Merging x86
@ECHO -------------------------
@ECHO.
@ECHO.
set sTbin=bin\x86
set tbin=%t%\%sTbin%
copy History.txt %sTbin%
cd src\cde\bin\Release
%ilmerge% /targetplatform:"%NET461%" /target:cde /out:%tbin%\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
copy cde.exe.config %tbin%

cd %t%

cd src\cdeWin\bin\Release
%ilmerge% /targetplatform:"%NET461%" /target:cde /out:%tbin%\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll Util.dll
cd %t%

@rem @ECHO Running Unit Tests
@rem ..\lib\NUnit-2.5.10..11092\bin\net-2.0\nunit-color-console.exe .\cdeLibTest\bin\Debug\cdelibtest.dll /nologo

:END
@pause
