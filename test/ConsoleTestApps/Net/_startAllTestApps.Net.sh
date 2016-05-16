#!/bin/bash

echo "start service"
gnome-terminal -x mono bin/Release/Test1.Service.exe  10           40001 JSON

echo "start client"
gnome-terminal -x mono bin/Release/Test1.Client.exe   10 localhost:40001 JSON

read -p "Test1 started..." inVar
