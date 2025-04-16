## ioSender - a gcode sender for grblHAL and Grbl controllers

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

8-bit Arduino controllers needs _Toggle DTR_ selected in order to reset the controller on connect. Behaviour may be erratic if not set.

![Toggle DTR](Media/Sender8.png)

#### Edge pre-releases

Edge pre-releases can be [downloaded from here](https://www.io-engineering.com/downloads), they contains changes yet to be incorporated in a main release and might be buggy and even break existing functionality.  
Use with care and please [post feedback](https://github.com/terjeio/ioSender/discussions/436) on any issues encountered!

2.0.46p7:

* Moved lathe wizards to new tabs under top level tab _Lathe wizards_.

* Added backup of offsets and tool table to the backup available in _Settins: Grbl".
Offsets and tools is saved to the application folder in the file _offsets.nc_, this file has to be loaded and run manually when restoring.  
Ref. discussion [#448](https://github.com/terjeio/ioSender/discussions/448).

2.0.46p6:

* Improved handling of upload to controller file systems (SD card, littlefs).

* Updated to handle parsing of additional IP addresses that may be reported from the controller.

* Fixed handling of alarms, added description to alarm codes reported in status line and console log.

2.0.46p5:

* Added _App_ setting _Send comments_, tick if the controller makes use of "magic" comments for functionality. Currently the Plasma plugin may do so.

2.0.46p4:

* Added [command line parameter](https://github.com/terjeio/ioSender/wiki/Setup-and-configuration#optional-command-line-parameters) `-configpath` for specifying which directory to use for configuration files.

* Now displays message if saving to configuration files fails. Ref. issue [#424](https://github.com/terjeio/ioSender/issues/424).

2.0.46p3:

* Fixed regression introduced in p1, external edge probing fail. Ref. issue [#438](https://github.com/terjeio/ioSender/issues/438).

2.0.46p2:

* Added support for latest \(build 20250204\) grblHAL Trinamic plugin, it now outputs StallGuard results for both motors when ganging/auto-squaring is enabled.  
__NOTE:__ Previous versions of ioSender will crash when plotting StallGuard results for ganged/auto-squared axes.

2.0.46p1:

* Fix for center finder failing when probing more than one pass. Ref. issue [#434](https://github.com/terjeio/ioSender/issues/434).

* Support for XY touch plate, adds checkbox to XY offsets in the _Probing tab_. Ref. issue [#432](https://github.com/terjeio/ioSender/issues/432).

* Initial changes for using a probe offset from the spindle for Center and Height map probing.  
__NOTE:__ This has not been fully completed/tested, I need to get my test machine updated for it. Ref. issue [#405](https://github.com/terjeio/ioSender/issues/405).

* Improved SD card handling. Requires grblHAL controller with a build date >= 20250128.

#### General

If you want to test ioSender with grblHAL but do not have a board yet you can use the [grblHAL simulator](https://github.com/grblHAL/Simulator).
Build it with the [Web Builder](https://svn.io-engineering.com:8443/?driver=Simulator&board=Windows), unpack the .exe-files in the downloaded .zip somewhere and
open a command window (cmd or PowerShell) in the folder by \<Shift\>+Right clicking in it, select _Open PowerShell window here_ or
_Open command window here_ from the popup menu to open it.
Then find your computers IP address by typing `ipconfig` - the IP address can be found in the report generated.  
Run the simulator by typing `./grblHAL_sim -p 23` - 23 is the default Telnet port number and you may have to change it if a Telnet server is already running on the machine.
Leave the window open.  
Now start ioSender and select the _Network_ tab in the sender connection dialog, change the port number if you run the simulator with a different port,
type in your computers IP address and click _Ok_ to connect.  
You can run gcode programs, jog, access settings etc. but _not_ use gcodes that needs input - e.g. probing.  
The simulator can be stopped by typing \<Ctrl\>+C in the command window or by closing it.

---

Latest release is 2.0.45, see the [changelog](changelog.md) for details. 

---

Some UI examples:

![Sender](Media/Sender.png)

Main screen.
<br><br>

![3D view](Media/Sender2.png)

3D view of program, with live update of tool marker.
<br><br>

![3D view](Media/Sender2_XL.png)

XL version, German translation.
<br><br>

![Jog flyout](Media/Sender7.png)

Jogging flyout, supports up to 9 axes. The sender also supports keyboard jogging with \<Shift\> \(speed\) and \<Ctrl\> \(distance\) modifiers.
<br><br>

![Easy configuration](Media/Sender3.png)

Advanced grbl configuration with on-screen documentation. UI is dynamically generated from data in a file and/or from the controller.
<br><br>

![Probing options](Media/Sender4.png)

Probing options.
<br><br>

![Easy configuration](Media/Sender5.png)

Lathe mode.
<br><br>

![Easy configuration](Media/Sender6.png)

Conversational programming for Lathe Mode. Threading requires [grblHAL](https://github.com/grblHAL) controller with driver that has spindle sync support.

---
2025-03-13
