#!/bin/bash

cd ./../ConsoleTestApps/Net/bin/Debug
echo "start service"
gnome-terminal -x mono Test1.Service.exe

read -p "Browser test started..." inVar
