AmpyFileManager
---------------
Windows GUI for the Adafruit MicroPython Utility

A simple GUI wrapper that executes the AMPY command to manipulate the files on an ESP8266 board running MicroPython.

It was written in C# in Visual Studio 2017, so you will need VS Express or better to compile it.  It's only external 
dependency (besides Python and Ampy) is the Scintilla editor control (https://github.com/jacobslusser/ScintillaNET), 
which allows for Python3 syntax highlighting.

As a development tool, I wrote the utility to mainly just edit the files directly off of the device.  I have also 
embedded a simple terminal emulator to send commands to the serial REPL.

A precompiled binary is available for download here:

  http://wezensky.no-ip.org/shared/afm.zip
  
Just unzip to a convenient location and run the AmpyFileManager.exe. NOTE: The latest .Net Framework is required 
for this application to run.

HOW TO START:

- Plug your MicroPython device into your computer and determine what com port it was assigned to
- Start the application.
- Select the com port for your device (if there is more than one)
- The main window should appear with the files on the root of the device listed
- If there was a problem and the files were not listed, click on the Refresh button

HOW TO USE:

All the features are pretty self-explanatory, but here is just a short description of it's general use.

- Navigation
  - To open a file for viewing or editing, select the file and click "Open" (or just double-click the filename)
  - To go into a sub-folder select the folder and click "Open" (or just double-click the folder name)
  - To go back one directory click on the [..] entry at the top of the file list
- Commands
  - "New" will prepare a new file for editing
  - "Open" will open a file for editing or change the directory
  - "Load" will allow you to import a file from your computer
  - "Export" will save the selected file to your computer
  - "Delete" will delete the file from the device
  - "Move" will move (rename) the selected file
  - "MKDIR" will allow you to create a sub-folder
  - "Refresh" will re-read the file list of the current directory

ADDITIONAL INFO:

- Configuration setting are located in the AmpyFilemanager.exe.config file
  - if <b>ExternalTerminal</b> is set to "Y" the <b>TerminalApp</b> and <b>TerminalAppArgs</b> settings are used
    - <b>TerminalApp</b> is the EXE to to run
    - <b>TerminalAppArgs</b> are the arguments to run the terminal app with
      - The term {PORT} in the <b>TerminalAppArgs</b> setting will be replaced at runtime with the current port
    - Example:

        <p>
        &lt;add key="ExternalTerminal" value="Y" /&gt;<br />
        &lt;add key="TerminalApp" value="putty" /&gt;<br />
        &lt;add key="TerminalAppArgs" value="-load &quot;repl&quot; -serial {PORT}" /&gt;<br />
        </p>
        
        Invokes the putty.exe application and uses the "repl" session
  - The <b>EditExtensions</b> setting determines what types of files are editable (text)
  - <b>UniqueSessions</b> indicates if a single session directory is used or a new one for each program start
    - The "session" directory is where the file being edited is held temporarily
  - The remaining settings are self-explanatory

CAVEATS:

- This editor is only meant to edit a single file at a time
- Because of some limitations, in order to use this tool you must follow this guideline...
    - Directories will be recognized by their zero size reported
    - Editable files are recognized by their use of an extension 
- Although it should work with any device that AMPY works with, it has only been tested with a Wemos D1 Mini 
  a                                                                                                                                                          nd a Witty Cloud Board
- This is mainly for text files (binary files will upload to the device but will not download correctly)
- Switching between the console and the editor (and back) is a little rough.  
    - You may have to "Refresh" or try again to get a feature to work.
    - Sometimes the software will pause until the device is momentarily unplugged
    - This is highly dependent on the type of application that is running
