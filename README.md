# SensorCommExample

Example code on how to communicate with the infrared thermometer sensor.

This program Searches all comm ports for sensors (by default, slaveId=1~16), and reads the target temperature and internal temperature of each sensor 100 times.

Language: C#

IDE: Visual Studio

Usage:

    SensorCommExample.exe 247 

to search for slaveId from 1 to 247 (default is 16)
