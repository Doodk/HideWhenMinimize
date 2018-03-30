# HideWhenMinimize

This project uses C# language.

This program hides window into the taskbar when it is minimized. If the window has a tray icon, it will minimize to tray icon instead.


The project will setup a listener attatching to the target process, and it will quit after the target process quits.
If the target process is not found, the project will start the target process at first (if have full path in setting file's Path tag).


command line argument: [-force | -hide]

-force : end the old listener forcibly (if exist), re-attach the listener to the target process.

-hide  : hide the target window after found.


The listener will be setup by a xml setting file.
Here is a sample setting file:

File name: <HideWhenMinimize.xml>
```xml
<Hook>
	<Path>C:\Program Files (x86)\Windows Live\Mail\wlmail.exe</Path>
	<MaxWait>12</MaxWait>
	<WinName> - Windows Live Mail</WinName>
	<WinClass>Outlook Express Browser Class</WinClass>
	<InfiniteLoop>false</InfiniteLoop>
</Hook>
```
Path: <MUST HAVE> full path for the process, or only the exe file name such as "wlmail.exe"
      (If it is not the full path, the program will not start this process since it is not found.)

MaxWait: [in second] the maximum wait(loop) time to look for the specific window's name after the process (from path tag) has been found.

WinName: determines which window to be hidden. It can be a partial name. If this tag is missing or blank, the program will look for the process' main window (may not be accurate).

WinClass: determines which window class will be looking for, uses to narrow the range, and makes the searching more accurate and faster.

InfiniteLoop: [true/false] if it is true, it will constantly looking for the process and the WinName.
