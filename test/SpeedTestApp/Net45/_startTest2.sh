#!/bin/bash

echo start catalog service
sh -c "mono ../../../src/Remact.Catalog/bin/Release.Mono/Remact.Catalog.exe" &

echo start service
sh -c "mono bin/Release/Test2.Service.exe" &

echo start client
sh -c "mono bin/Release/Test2.Client.exe" &

read -p "Test2 started..." inVar

