## GRBL GCode Sender

---

2020-01-22: First alpha release of binary. Use with care and please report issues!

__NOTE:__ Keyboard jogging with a grbl firmware other than grblHAL is likely to fail, this is a firmware problem so no plan to fix. grblHAL _may_ also occasionally fail \(likely due to issues in the sender\) so be ready to hit reset/estop if it goes wrong!

__NOTE:__ .Net Runtime version 4.5.2 is required, so this sender is for Win7 or later.

Some known issues:

* The 3D-viewer will slow down loading of large files, if this is a problem then disable it in app settings. A rewrite is planned, using a faster library.

* A restart is required after changing most app settings.

* Lathe mode wizards may generate bad code, needs comprehensive testing. NOTE: As far as I know currently only grblHAL for MSP432 supports G76 for threading, all other builds will generate an error on G76. The G76 code generated is the linuxCNC variant.

* There is no UI yet for configuring streaming via telnet or websocket protocols, either the app config file must be edited or the connection parameter has to be supplied on the command line.

* Program limits displayed does not include arcs correctly if outside the bounding box defined by linear moves.

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

---

A complete rewrite of my [Grbl CNC Controls library](https://github.com/terjeio/Grbl_CNC_Controls) including a sender application on top of these. It supports new features in [GrblHAL](https://github.com/terjeio/grblHAL) such as manual tool change and [external MPG](https://github.com/terjeio/GRBL_MPG_DRO_BoosterPack) control - and is one of the reasons for writing this library and app. Other senders I have tried does not play nice when a MPG pendant is connected directly to the Grbl processor card...

---


Current layout, most likely to change. Since using the VMMV coding pattern this is fairly easy to do.

![Sender](Media/Sender.png)

---
2020-01-22
