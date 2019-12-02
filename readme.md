## GRBL GCode Sender

A complete rewrite of my [Grbl CNC Controls library](https://github.com/terjeio/Grbl_CNC_Controls) including a sender application on top of these. It supports new features in [GrblHAL](https://github.com/terjeio/grblHAL) such as manual tool change and [external MPG](https://github.com/terjeio/GRBL_MPG_DRO_BoosterPack) control - and is one of the reasons for writing this library and app. Other senders I have tried does not play nice when a MPG pendant is connected directly to the Grbl processor card...

Please note that this is a __code preview release only__, as I am still learning WPF and VMMV coding patterns there may be significant changes to the codebase. A binary release is planned when testing is completed.

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

---

Current layout, most likely to change. Since using the VMMV coding pattern this is fairly easy to do.

![Sender](Media/Sender.png)

---
2019-12-02
