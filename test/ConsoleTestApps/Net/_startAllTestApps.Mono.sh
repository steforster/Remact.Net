#!/bin/bash
#
# you may have installed a parallel mono environment using the script from http://patrick.qmtech.net/blog/?p=14
# this command sets the environment variables.
echo "use newest mono environment if available"
source mono-2.10-environment

cd bin/mono
echo "start router"
sh -c "mono ../../../WcfRouter/bin/mono/SF.AsyncWcfLib.Router.exe" &

echo "start 3 active services"
gnome-terminal -x mono Test1.ServiceActive.exe 3
gnome-terminal -x mono Test1.ServiceActive.exe 2
gnome-terminal -x mono Test1.ServiceActive.exe 1

echo "start service"
gnome-terminal -x mono Test1.Service.exe

echo "start 4 types of clients"
gnome-terminal -x mono Test1.TwoClients.exe
gnome-terminal -x mono Test1.ClientWinForm.exe
gnome-terminal -x mono Test1.Client.exe
gnome-terminal -x mono Test1.ClientNoSync.exe

read -p "Test1 started..." inVar
