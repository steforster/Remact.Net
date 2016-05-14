#!/bin/bash

echo start catalog service
sh -c "mono ../../../src/Remact.Catalog/bin/Release.Mono/Remact.Catalog.exe" &

echo start service
sh -c "mono bin/Release/Remact.SpeedTest.Service.Mono.exe" &

echo start client
sh -c "mono bin/Release/Remact.SpeedTest.Client.Mono.exe" &

read -p "SpeedTest started..." inVar

