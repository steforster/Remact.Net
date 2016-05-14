#!/bin/bash

echo "start service"
gnome-terminal -x mono bin/Release/Test1.Service.exe

echo "start client"
gnome-terminal -x mono bin/Release/Test1.Client.exe

read -p "Test1 started..." inVar
