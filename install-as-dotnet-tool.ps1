#!/usr/bin/env pwsh

function Test-CommandExists([Parameter(Mandatory)] [string] $command)
{
    try { 
        if (Get-Command $command -ErrorAction Stop) { return $true } 
    } catch {
        return $false
    }
}


dotnet pack ./src -o ./nupkg

$exists = $(Test-CommandExists script-launcher)
$action = $exists ? 'update' : 'install'

dotnet tool $action -g ScriptLauncher --add-source ./nupkg --ignore-failed-sources

