#!/bin/bash

echo start catalog service
sh -c "mono ../../../src/bin/Release/Remact.Net.CatalogApp.exe" &

echo start service
sh -c "mono bin/Release/SpeedTest.Service.exe" &

echo start client
sh -c "mono bin/Release/SpeedTest.Client.exe" &

read -p "SpeedTest started..." inVar

