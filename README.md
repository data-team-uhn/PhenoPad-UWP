PhenoPad-UWP
============
[PhenoPad](http://www.phenopad.ai/) is a note taking application that allows physicians to take free-form notes and capture standard phenotypic information via audio, photo and video.

<p align="center">
  <img src="https://github.com/jixuan-wang/PhenoPad-UWP/blob/master/PhenoPad/Assets/features_demo.png?raw=true" alt="drawing" width="800"/>
<p />


Features
--------
* Free-form note-taking by handwriting or typing. 
* Insert drawings, images to note and capture photos and videos.
* Real-time handwriting to text conversion.
* Real-time speech-to-text and speaker diarization.
* Medical term recognition on notes and ASR transcripts. 
* Phenotypes suggestion based on transcripts and differential diagnosis on diseases.
* Edit and annotate EHR.

Running the app
----------------
The app requires Microsoft Windows 10. You also need to download the [Visual Studio IDE](https://visualstudio.microsoft.com/).

Clone the project to your device. In Visual Studio, open the project by `File`->`Open`->`Project/Solution...` or press `Ctrl+Shift+O` and select the project's solution (PhenoPad.sln). Use the green start button to build and run the app.

More detailed guide on how to use the app in Wiki.

Speech Service
-----------------------
You need to set up your own speech server to use the speech features. A demo server and the instructions to set it up is available at https://github.com/haochiz/PhenoPad-SpeechEngine.

Once you set up the server and have it running, click the gear button on the top bar and select 'Settings'. Select the `Surface Microphone` option. Type "your.server.ip:port" (e.g. `127.0.0.1:8888`) in the "ASR server" field then click `Change Server`. You should see a notification indicating ASR server address has been changed. 

Click the microphone icon in the bottom left corner to start/stop a speech session, click the converastion icon on top left to open the real-time conversation panel to see the results.

Phenotips Service
-----------------
By default, the phenotype suggestion and disease prediction service which relies on [PhenoTips](https://phenotips.com/) is hosted on our own server. If you'd like to set up your own service, you can install the latest stable Phenotips branch at https://github.com/phenotips/phenotips/tree/phenotips-1.4.9. Then go to Application Settings (click the gear button on the top bar) and change the address for `Suggestion`, `Differential` and `PhenoDetail` to the address of your own server.
