# SensorCommExample

Example code on how to communicate with the infrared thermometer sensor

Currently, this program Searches all comm ports for sensors (slaveId=1~16), and reads the temperature of each sensor 100 times.

Usage:
    SensorCommExample.exe 247
    to search for slaveId from 1 to 247 (default is 16)
