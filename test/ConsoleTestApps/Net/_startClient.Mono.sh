#!/bin/bash

cd bin/Release
echo "start service"
gnome-terminal -x mono Test1.Service.exe

echo "start client"
gnome-terminal -x mono Test1.Client.exe

read -p "Test1 started..." inVar
