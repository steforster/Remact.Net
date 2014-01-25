#!/bin/bash
#echo start router
#sh -c "mono ../WcfRouter/bin/mono/SF.AsyncWcfLib.Router.exe" &

echo start service
sh -c "mono bin/Release/Test2.Service.exe" &

echo start client
sh -c "mono bin/Release/Test2.Client.exe" &

read -p "Test2 started..." inVar

