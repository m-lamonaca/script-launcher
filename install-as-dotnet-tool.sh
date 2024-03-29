#!/usr/bin/env bash

dotnet pack ./src -o ./nupkg

EXISTS=$(command -v scrl)

if [ "$EXISTS" ]; then 
    ACTION="update"
else 
    ACTION="install" 
fi

dotnet tool "$ACTION" -g ScriptLauncher --add-source ./nupkg --ignore-failed-sources
