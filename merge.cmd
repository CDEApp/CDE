cd src\cde\bin\Release
"c:\Program Files (x86)\Microsoft\ILMerge\ILMerge.exe" /targetplatform:v4,c:\windows\Microsoft.Net\Framework\v4.0.30319 /target:cde /out:..\..\..\..\bin\cde.exe cde.exe cdelib.dll AlphaFS.dll protobuf-net.dll Autofac.dll Mono.Terminal.dll
copy cde.exe.config ..\..\..\..\bin
cd ..\..\..\..

cd src\cdeWin\bin\Release
"c:\Program Files (x86)\Microsoft\ILMerge\ILMerge.exe" /targetplatform:v4,c:\windows\Microsoft.Net\Framework\v4.0.30319 /target:cde /out:..\..\..\..\bin\cdewin.exe cdewin.exe cdelib.dll AlphaFS.dll protobuf-net.dll
cd ..\..\..\..

