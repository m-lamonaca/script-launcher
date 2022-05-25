dotnet pack ./src -o ./nupkg
dotnet tool install -g ScriptLauncher --add-source ./nupkg --ignore-failed-sources