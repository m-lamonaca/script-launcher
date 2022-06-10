dotnet pack ./src -o ./nupkg

$exists = $(Test-CommandExists script-launcher)
$action = $exists ? 'update' : 'install'

dotnet tool $action -g ScriptLauncher --add-source ./nupkg --ignore-failed-sources

