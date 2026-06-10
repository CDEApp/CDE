# Deveoper Notes

## Building the app

The build uses Fallout to perform the steps. (It replaced Nuke.)

The build definition lives in `build/Build.cs` (built on `Fallout.Common`), and the
`build.cmd` / `build.ps1` / `build.sh` scripts bootstrap `build/_build.csproj`.
Fallout config, parameters and temp/log output are kept under `.fallout/`.

To build the app on windows run:

```shell
build.cmd publish
```

On Linux/macOS run:

```shell
./build.sh publish
```

Artifacts from the build will be built to `.\artifacts`

