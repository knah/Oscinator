### 1.0.2 changes:
* Added CVR ApplicationSupport class
* Moved some activity to background threads to slightly reduce UI freezes
* Avoid getting stuck on a wrong single process being selected when switching remote apps with downtime between them


### 1.0.1 changes:
UI improvements:
* Parameter table sort is now case-insensitive
* The vertical scrollbar on the parameter table is now always fully visible for ease of use in VR

Various fixes to multi-remote use cases:
* Non-VRC applications no longer use VRC support class
* Fix process selector not appearing when multiple remotes are detected
* Fix OSC messages not being properly attributed based on the process owning the sending socket
* Improve process name detection on Linux
* Announce/query mDNS more often to help with detection on/of lazier clients

Other fixes:
* Disable HTTP keep-alive on OSCQuery server, as it led to connection resets in CVR
