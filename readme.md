## ioSender - a gcode sender for grblHAL and Grbl controllers

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

8-bit Arduino controllers needs _Toggle DTR_ selected in order to reset the controller on connect. Behaviour may be erratic if not set.

![Toggle DTR](Media/Sender8.png)

#### Edge pre-releases

Edge pre-releases can be [downloaded from here](https://www.io-engineering.com/downloads), they contains changes yet to be incorporated in a main release and might be buggy and even break existing functionality.  
Use with care and please [post feedback](https://github.com/terjeio/ioSender/discussions/436) on any issues encountered!

2.0.47p6:

* Fix for cannot close XL version when MPG mode is active. Ref. issue [#499](https://github.com/terjeio/ioSender/issues/499#issuecomment-3910302756).

* Added some special words that can be used to send real time commands from macros:  
`{park}` or `{safetydoor}` - send safety door command.  
`{optionalstop}` - toggle optional stop switch.  
`{singleblock}` - toggle single block switch.  
`{probeconnected}` - toggle probe connected state.  
<br>Whether or not these commands are acted upon depends on the controller configuration.
> [!NOTE]
> When used the macro has to contain only one special word and no other code. 

* Added coordinate selector and associated _Go_ button to the _Goto_ flyout/panel that will execute a rapid move to the selected coordinate position. Use with care!

* Added `JogBaseControl.KeyJogCancel` method that _has to be added_ on the _up_ event for keys used for triggering UI jog buttons when in _Continuous_ mode.

* Added support for adding or modifying direct keyboard jog keycodes in the keymap file.  
An example for the B axis:
```
  <KeyMapping>
    <Key>D</Key>
    <Method>Jogkey.Bplus</Method>
  </KeyMapping>
  <KeyMapping>
    <Key>T</Key>
    <Method>Jogkey.Bminus</Method>
  </KeyMapping>
```
> [!NOTE]
> Any keys mapped like this will override other mappings for the same keys when pressed alone, in combination with _\<Ctrl\>_ or in combination with _\<Shift\>_.  
> Adding support for adding or modifying direct keyboard jog keycodes required a rather large refactoring of the keypress handler. Please report any odd behaviour!

2.0.47p5:

* [PR #491](https://github.com/terjeio/ioSender/pull/491) merged. Adds spindle dir combobox in threading wizard.

* Added option to set "breakpoints" in programs that will halt execution with a feed hold \(M0\). Press _\<Cycle Start\>_ to continue after a break.
Breakpoints are set via the popup menu available in the program listing.

* Added option to add line numbers to gcode programs that does not have them. Enable in the _Settings: App_ tab, _Main_ group.
If the controller is configured to output line numbers and line numbers are present in the program the current executing line will be flagged with "@" in the program listing.

* Fix for arcs not displaying correctly when relative motion is active. Ref. issue [#499](https://github.com/terjeio/ioSender/issues/499).

* Added methods to allow mapping shortcut keys for jogging all axes. Ref. discussion [#494](https://github.com/terjeio/ioSender/discussions/494).

* Added option to select continuous jog in the jog UI, enabled touch control. Ref. issue [#498](https://github.com/terjeio/ioSender/issues/498) and [#468](https://github.com/terjeio/ioSender/issues/468).

* Added support for navigating SD card directories. Note that this feature requires the controller to be configured to handle it, a $650 option for grblHAL.

* Spindle can now be stopped in _Hold_ state.

* Tool table is now sorted numerically, added tool name to listing when available from the controller.

* Internal changes, some are work in progress such as full support for rotation commands and flow control.

2.0.47p4:

* Bug fix.

2.0.47p3:

* Camera view crosshair can be moved (dragged) by \<SHIFT\> left clicking the left mouse button at the cross intersection to compensate for parallax. \<SHIFT\> right click to restore it to the center.
Ref. [discussion 484](https://github.com/terjeio/ioSender/discussions/484).

* Added parser support for G33.1 and G84.

* Updated _Tools_ tab, list will now show tool name if available from the controller. Changed sort order to numeric.

* Fixed some minor bugs.

2.0.47p2:

* Allow closing the app when running a job from SD card. Ref. [discussion comment](https://github.com/terjeio/ioSender/discussions/335#discussioncomment-14517412).
Reconnecting while it is running may terminate the job depending on the controller, if not terminated the app will not be initialized correctly until the _Reset_ button has been pressed.

* Fix for incorrect 3D rendering or arcs when negative scaling (G51) is active.

* Improved keyboard mappings, new can mappings be added and existing ones can be removed by setting the _Method_ to "_None_". Ref. issue [#472](https://github.com/terjeio/ioSender/issues/476)

* Fixed handling of serial port handshake (RTS or Xon/Xoff), the app config file has to be edited manually to set it.

2.0.47p1:

* Fix for regression causing "Start from here" and other menu items in the program listing popup to fail with exception. Ref. disussion [#469](https://github.com/terjeio/ioSender/discussions/469).

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

Latest release is [2.0.46](https://github.com/terjeio/ioSender/releases/tag/2.0.46), see the [changelog](changelog.md) for details. 

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
2025-11-11
