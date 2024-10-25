# Script Launcher

Find executable scripts in a directory and allow to select which one to execute.

## Installation

### As .NET CLI Tool

To be installed as a tool, you need the [.NET CLI][CLI] which is included in the [.NET SDK][SDK]

Install manually using the following commands or by using the provided installation scripts.

```sh
dotnet pack ./src -o ./nupkg
dotnet tool install -g ScriptLauncher --add-source ./nupkg --ignore-failed-sources
```

### As a Standalone Executable

You will need the identifier of the .NET Runtime ([RID]) and the Target Framework Moniker ([TFM]) for your OS and Runtime.  

```sh
# self contained: will not need the .NET runtime to function, bigger resulting size
dotnet publish -c Release --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -f <TFM> -r <RID> -o <output-directory> ./src

# no self contained: will need the .NET runtime to be installed to function, smallest size
dotnet publish -c Release --no-self-contained -p:PublishSingleFile=true -r <RID> -o <output-directory> ./src
```

_**NOTE**_: The option `-p:PublishTrimmed=true` may produce some *warnings*. If so simply skip that option and the resulting executable will be larger

## Usage

```sh
USAGE:
    scrl [path] [OPTIONS]

ARGUMENTS:
    [path]    Starting directory (Default: .)

OPTIONS:
                                     DEFAULT
    -h, --help                                  Prints help information
    -x, --extensions <EXTENSIONS>               List of script extensions to search for
    -d, --depth                      3          Search depth
    -e, --elevated                              Run with elevated privileges
    -g, --group                                 Group scripts by folder
    -b, --brief                                 Show brief information
```

[CLI]: https://docs.microsoft.com/en-us/dotnet/core/tools/ ".NET CLI Docs"
[SDK]: https://dotnet.microsoft.com/en-us/download ".NET SDK Downloads"
[RID]: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog "Runtime IDs Catalog"
[TFM]: https://docs.microsoft.com/en-us/dotnet/standard/frameworks "Target Framework Moniker Docs"
