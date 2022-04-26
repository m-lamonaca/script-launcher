function Get-Runtime
{
    if($isLinux)
    {
        return "linux-x64";
    }

    if($isMac)
    {
        return "osx-x64";
    }

    if($isWindows)
    {
        return "win-x64";
    }

    return $null;
}

$runtime = Get-Runtime;

if($runtime -ne $null)
{
    dotnet publish -o out -c Release --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -f net6.0 -r $runtime .\src\
}