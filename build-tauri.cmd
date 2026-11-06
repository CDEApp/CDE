@echo off
rem Builds the Tauri desktop app (cde-tauri + cdeApi sidecar) into artifacts\cde-tauri.
rem Needs Node, pnpm and Rust. Extra arguments are passed through to the build.
call "%~dp0build.cmd" PublishTauri %*
exit /b %ERRORLEVEL%
