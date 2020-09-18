## GRBL GCode Sender

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

---

Latest changes committed 20200918, see the [changelog](changelog.md) for details.

---

A complete rewrite of my [Grbl CNC Controls library](https://github.com/terjeio/Grbl_CNC_Controls) including a sender application on top of these. It supports new features in [GrblHAL](https://github.com/terjeio/grblHAL) such as manual tool change and [external MPG](https://github.com/terjeio/GRBL_MPG_DRO_BoosterPack) control - and is one of the reasons for writing this library and app. Other senders I have tried does not play nice when a MPG pendant is connected directly to the Grbl processor card...

---

Some UI examples:

![Sender](Media/Sender.png)

Main screen.
<br><br>

![3D view](Media/Sender2.png)

3D view of program, with live update of tool marker.
<br><br>

![Jog flyout](Media/Sender7.png)

Jogging flyout, supports up to 6 axes. The sender also support keyboard jogging with \<Shift\> \(speed\) and \<Ctrl\> \(distance\) modifiers.
<br><br>

![Easy configuration](Media/Sender3.png)

Advanced grbl configuration with on-screen documentation. UI is dynamically generated from data in a file.
<br><br>

![Probing options](Media/Sender4.png)

Probing options.
<br><br>

![Easy configuration](Media/Sender5.png)

Lathe mode.
<br><br>

![Easy configuration](Media/Sender6.png)

Conversational programming for Lathe Mode.

---
2020-09-18
