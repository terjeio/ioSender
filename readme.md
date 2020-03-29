## GRBL GCode Sender

2020-03-29: Alpha 14 release of binary.

Added probing tab with tool length, edge finder, center finder and height map options. __NOTE:__ This has NOT been extensively tested! G20 (inches) mode not tested at all! Do not use unless you understand the risks.

Disabled keyboard jogging when firmware is not [grblHAL](https://github.com/terjeio/grblHAL). May add setup option to reeanble later.

Many internal changes.

---

2020-03-16: Alpha 13 release of binary.

Program list control has been replaced vith a tab control with easy access to a 3D view and a console (for showing replies from grbl).

Added bounding box calculation for arcs (G2 & G3), _Program limits_ should now be accurate.

---

2020-01-22: First alpha release of binary. Use with care and please report issues!

__NOTE:__ Keyboard jogging with a grbl firmware other than grblHAL is likely to fail, this is a firmware problem so no plan to fix. grblHAL _may_ also occasionally fail \(likely due to issues in the sender\) so be ready to hit reset/estop if it goes wrong!

__NOTE:__ .Net Runtime version 4.5.2 is required, so this sender is for Win7 or later.

Some known issues:

* The 3D-viewer will slow down loading of large files, if this is a problem then disable it in app settings. __Fixed.__

* A restart is required after changing most app settings.

* Lathe mode wizards may generate bad code, needs comprehensive testing. NOTE: As far as I know currently only grblHAL for MSP432 supports G76 for threading, all other builds will generate an error on G76. The G76 code generated is the linuxCNC variant.

* There is no UI yet for configuring streaming via telnet or websocket protocols, either the app config file must be edited or the connection parameter has to be supplied on the command line.

* Program limits displayed does not include arcs correctly if outside the bounding box defined by linear moves. __Fixed.__

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

---

A complete rewrite of my [Grbl CNC Controls library](https://github.com/terjeio/Grbl_CNC_Controls) including a sender application on top of these. It supports new features in [GrblHAL](https://github.com/terjeio/grblHAL) such as manual tool change and [external MPG](https://github.com/terjeio/GRBL_MPG_DRO_BoosterPack) control - and is one of the reasons for writing this library and app. Other senders I have tried does not play nice when a MPG pendant is connected directly to the Grbl processor card...

---


Current layout, likely to change. Since using the MVVM coding pattern this is fairly easy to do.

![Sender](Media/Sender.png)

3D view of program, with live update of tool marker.

![3D view](Media/Sender2.png)

Advanced grbl configuration with on-screen documentation. UI is dynamically generated from data in a file.

![Easy configuration](Media/Sender3.png)

---
2020-03-16
