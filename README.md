# AmpyFileManager
Windows GUI for the Adafruit MicroPython Utility

A simple GUI wrapper that executes the AMPY command to manipulate the files on an ESP8266 board running MicroPython.

It was written in C# in Visual Studio 2015, so you will need VS Express or better to compile it.  It's only external dependency (besides Python and Ampy) is the Scintilla editor control (https://github.com/jacobslusser/ScintillaNET), which allows for Python3 syntax highlighting.

As a development tool, I wrote the utility to mainly just edit the files directly off of the device.  I have also embedded a simple terminal emulator to send commands to the serial REPL.
