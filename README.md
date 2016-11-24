# AmpyFileManager
Windows GUI for the Adafruit MicroPython Utility

A simple GUI wrapper that executes the AMPY command to manipulate the files on an ESP8266 board running MicroPython.

It was written in C# in Visual Studio 2015, so you will need VS Express or better to compile it.  It's only external dependency (besides Python and Ampy) is the Scintilla editor control (https://github.com/jacobslusser/ScintillaNET), which allows for Python3 syntax highlighting.

As a development tool, I wrote the utility to mainly just edit the files directly off of the device.  I have also embedded a simple terminal emulator to send commands to the serial REPL.

A precompiled binary is available for download here:

  https://dl.dropboxusercontent.com/u/112915/AmpyFileManager2.zip
  
Just unzip to a convenient location and run the AmpyFileManager.exe. NOTE: The latest .Net Framework is required for this application to run.

HOW TO START:

- Plug your MicroPython device into your computer and determine what com port it was assigned to
- Start the application.
- Select the com port for your device (if there is more than one)
- The main window should appear with the files on the root of the device listed
- If there was a problem and the files were not listed, click on the Refresh button

HOW TO USE:

All the features are pretty self-explanatory, but here is just a short description of it's general use.

- To open a file for viewing or editing, select the file and click "Open" (or just double-click the filename)
- To go into a sub-folder select the folder and click "Open" (or just double-click the folder name)
- "Load" will allow you to import a file
- "Delete" will delete the file from the device

ADDITIONAL INFO:

- The backups are stored in a sub-folder of the directory where the EXE is located.
- Configuration setting are located in the AmpyFilemanager.exe.config file

CAVEATS:

- Although it should work with any device that AMPY works with, it has only been tested with a Wemos D1 Mini and a Witty Cloud Board
- This is mainly for text files (binary files have not been tested)
- Switching between the terminal and the file manager (and back) is a little rough.  You may have to "Refresh" or try again to get a feature to work.

