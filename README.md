# HideWhenMinimize

This project uses C# language.

This program can hide specific window during its minimize, which means when minimize this window(form), the window will hide in the taskbar. Further, if it has a tray icon, it will minimize to tray icon instead.


This program will setup a listener attatching to the target process, and it will quit after the target process quits.
If the target process is not found, the program will start the target process at first (if have full path in setting file's Path tag).


command line argument: [-force | -hide]
-force : end the old listener forcibly (if exist), re-attach the listener to the target process.
-hide  : hide the target window after found.


The listener will be setup by using a xml setting file, here is a sample setting file:

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
      (If it is not the full path, the program will not start this process if it is not found.)

MaxWait: [in second] the maximum wait(loop) time to look for the specific window's name after the process (from path tag) has been found.

WinName: to determine which window to be hide, it can be a partial name. If this tag is missing or blank, the program will look for the process' main window (may not be accurate).

WinClass: to determine which window class will be looking for, used to narrow the range, makes the searching more accurate and faster.
InfiniteLoop: [true/false] if it is true, it will constantly looking for the process and the WinName.
