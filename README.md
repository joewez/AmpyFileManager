# AmpyFileManager
Windows GUI for the Adafruit MicroPython Utility

A simple GUI wrapper that executes AMPY command to manipulate the files on an ESP8266 board running MicroPython.

It was written in C# in Visual Studio 2015, so you will need VS Express or better to compile it.  It's only exteral dependence is the Scintilla editor control (https://github.com/jacobslusser/ScintillaNET), which allows for Python3 syntax highlighting.

As a development tool, I wrote the utility to mainly just edit the files directly off of the device.  Switching between this utility and a terminal program (puTTY) I could switch between editing and testing/running very easily.
