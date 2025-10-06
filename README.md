QuestCameraKit is a collection of template and reference projects demonstrating how to use Meta Quest’s new **Passthrough Camera API** `(PCA)` for advanced AR/VR vision, tracking, and shader effects.

[![Follow on X](https://img.shields.io/twitter/follow/xrdevrob?style=social)](https://x.com/xrdevrob)
[![Join our Discord](https://img.shields.io/badge/Join-Discord-blue?style=social&logo=discord)](https://discord.com/invite/KkstGGwueN)

# Table of Contents
- [Overview](#overview)
- [Getting Started with PCA](#getting-started-with-pca)
- [Running the Samples](#running-the-samples)
- [General Troubleshooting & Known Issues](#general-troubleshooting--known-issues)
- [Acknowledgements & Credits](#acknowledgements--credits)
- [Community Contributions](#community-contributions)
- [News](#news)
- [License](#license)
- [Contact](#contact)

# Overview

## 1. 🎨 Color Picker

- **Purpose:** Convert a 3D point in space to its corresponding 2D image pixel.
- **Description:** This sample shows the mapping between 3D space and 2D image coordinates using the Passthrough Camera API. We use MRUK's EnvironmentRaycastManager to determine a 3D point in our environment and map it to the location on our WebcamTexture. We then extract the pixel on that point, to determine the color of a real world object.

## 2. 🍎 Object Detection with Unity Sentis

- **Purpose:** Convert 2D screen coordinates into their corresponding 3D points in space.
- **Description:** Use the Unity Sentis framework to infer different ML models to detect and track objects. Learn how to convert detected image coordinates (e.g. bounding boxes) back into 3D points for dynamic interaction within your scenes. In this sample you will also see how to filter labels. This means e.g. you can only detect humans and pets, to create a more safe play-area for your VR game. The sample video below is filtered to monitor, person and laptop. The sample is running at around `60 fps`.

| 1. 🎨 Color Picker                          | 2. 🍎 Object Detection                      |
|---------------------------------------------|---------------------------------------------|
| ![CPE](Media/ColorPicker_Environment.gif)   | ![OBJD](Media/ObjectDetection.gif)          |

## 3. 📱 QR Code Tracking with ZXing

- **Purpose:** Detect and track QR codes in real time. Open webviews or log-in to 3rd party services with ease.
- **Description:** Similarly to the object detection sample, get QR code coordinated and projects them into 3D space. Detect QR codes and call their URLs. You can select between a multiple or single QR code mode. The sample is running at around `70 fps` for multiple QR codes and a stable `72 fps` for a single code. Users are able to choose between CenterOnly and PerCorner raycasting modes via an enum in the inspector. This enables more accurate rotation tracking for use cases that require it (PerCorner), while preserving a faster fallback (CenterOnly).


## 4. 🪟 Shader Samples

- **Purpose:** Apply a custom shader effect to virtual surfaces.
- **Description:** A shader which takes our camera feed as input to manipulate the content behind it. Right now the project contains a Pixelate, Refract, Water, Zoom, Blur, GameBoy Green and VirtualBoy Red effect. Additionally examples for colorblindness red, green, blue and total have been added (Protanopia, Deuteranopia, Tritanopia, Achromatopsia). Frosted Glass shader is work in progress!

| 3. 📱 QR Code Tracking                | 4. 🪟 Shader Samples                  |
|---------------------------------------|---------------------------------------|
| ![QR Code](Media/QRCodeTracking.gif)  | ![Frosted](Media/ShaderSamples.gif)   |

## 5. 🧠 OpenAI vision model

- **Purpose:** Ask OpenAI's vision model (or any other multi-modal LLM) for context of your current scene.
- **Description:** We use a the OpenAI Speech to text API to create a command. We then send this command together with a screenshot to the Vision model. Lastly, we get the response back and use the Text to speech API to turn the response text into an audio file in Unity to speak the response. The user can select different speakers, models, and speed. For the command we can add additional instructions for the model, as well as select an image, image & text, or just a text mode. The whole loop takes anywhere from `2-6 seconds`, depending on the internet connection.

https://github.com/user-attachments/assets/a4cfbfc2-0306-40dc-a9a3-cdccffa7afea

## 6. 🎥 WebRTC video streaming

- **Purpose:** Stream the Passthrough Camera stream over WebRTC to another client using WebSockets.
- **Description:** This sample uses [SimpleWebRTC](https://assetstore.unity.com/packages/tools/network/simplewebrtc-309727), which is a Unity-based WebRTC wrapper that facilitates peer-to-peer audio, video, and data communication over WebRTC using [Unitys WebRTC package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html). It leverages [NativeWebSocket](https://github.com/endel/NativeWebSocket) for signaling and supports both video and audio streaming. You will need to setup your own websocket signaling server beforehand, either online or in LAN. You can find more information about the necessary steps [here](https://www.youtube.com/watch?v=-CwJTgt_Z3M)

## 7. 📅 Experimental QR Code detection

- **Purpose:** Detect and track QRCodes without having to use a third-party library.
- **Description:** As this feature is still in experimental state, make sure to use experimental mode on your Quest3 when testing. Unity usually asks for enabling that, before building. You can activate it via command line command using Meta Quest Hub too. More information can be found here - [MR Utility Kit QRCode Detection](https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-qrcode-detection) and here - [Mobile Experimental Features](https://developers.meta.com/horizon/documentation/native/android/mobile-experimental-features/)

| 6. 🎥 WebRTC video streaming | 7. 📅 Experimental QRCode detection |
|------------------------------|------------------------------------|
| <img src="Media/PCA_WebRTC.gif" width="500"/> | <img src="Media/QrCodeDetection.png" width="500"/> |

# Getting Started with PCA

| **Information**        | **Details**                                                                                                                                                                                             |
|------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Device Requirements**| - Only for Meta `Quest 3` and `3s`<br>- `HorizonOS v74` or later                                                                                                                                        |
| **Unity WebcamTexture**| - Access through Unity’s WebcamTexture<br>- Only one camera at a time (left or right), a Unity limitation                                                                                               |
| **Android Camera2 API**| - Unobstructed forward-facing RGB cameras<br>- Provides camera intrinsics (`camera ID`, `height`, `width`, `lens translation & rotation`)<br>- Android Manifest: `horizonos.permission.HEADSET_CAMERA`  |
| **Public Experimental**| - Apps using PCA are allowed to be submitted to the Meta Horizon Store since May 2025.                                                                                                                  |
| **Specifications**     | - Frame Rate: `30fps`<br>- Image latency: `40-60ms`<br>- Available resolutions per eye: `320x240`, `640x480`, `800x600`, `1280x960`                                                                     |

## Prerequisites

- **Meta Quest Device:** Ensure you are runnning on a `Quest 3` or `Quest 3s` and your device is updated to `HorizonOS v74` or later.
- **Unity:** Recommended is `Unity 6`. Also runs on Unity `2022.3. LTS`.
- **Camera Passthrough API does not work in the Editor or XR Simulator.**
- Get more information from the [Meta Quest Developer Documentation](https://developers.meta.com/horizon/documentation/unity/unity-pca-documentation)

> [!CAUTION]
> Every feature involving accessing the camera has significant impact on your application's performance. Be aware of this and ask yourself if the feature you are trying to implement can be done any other way besides using cameras.

## Installation
1. **Clone the Repository:**
   ```
   git clone https://github.com/xrdevrob/QuestCameraKit.git
   ```

2. **Open the Project in Unity:**
Launch Unity and open the cloned project folder.

3. **Configure Dependencies:**
Follow the instructions in the section below to run one of the samples.

# Running the Samples

## 1. **[Color Picker](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#-color-picker)**
- Open the `ColorPicker` scene.
- Build the scene and run the APK on your headset.
- Aim the ray onto a surface in your real space and press the A button or pinch your fingers to observe the cube changing it's color to the color in your real environment.

## 2. **[Object Detection with Unity Sentis](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#-object-detection-with-unity-sentis)**
- Open the `ObjectDetection` scene.
- You will need [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/get-started.html) for this project to run (com.unity.sentis@2.1.2).
- Select the labels you would like to track. No label means all objects will be tracked. <details>
  <summary>Show all available labels</summary>
  <table>
    <tr>
      <td>person</td>
      <td>bicycle</td>
      <td>car</td>
      <td>motorbike</td>
      <td>aeroplane</td>
      <td>bus</td>
      <td>train</td>
      <td>truck</td>
    </tr>
    <tr>
      <td>boat</td>
      <td>traffic light</td>
      <td>fire hydrant</td>
      <td>stop sign</td>
      <td>parking meter</td>
      <td>bench</td>
      <td>bird</td>
      <td>cat</td>
    </tr>
    <tr>
      <td>dog</td>
      <td>horse</td>
      <td>sheep</td>
      <td>cow</td>
      <td>elephant</td>
      <td>bear</td>
      <td>zebra</td>
      <td>giraffe</td>
    </tr>
    <tr>
      <td>backpack</td>
      <td>umbrella</td>
      <td>handbag</td>
      <td>tie</td>
      <td>suitcase</td>
      <td>frisbee</td>
      <td>skis</td>
      <td>snowboard</td>
    </tr>
    <tr>
      <td>sports ball</td>
      <td>kite</td>
      <td>baseball bat</td>
      <td>baseball glove</td>
      <td>skateboard</td>
      <td>surfboard</td>
      <td>tennis racket</td>
      <td>bottle</td>
    </tr>
    <tr>
      <td>wine glass</td>
      <td>cup</td>
      <td>fork</td>
      <td>knife</td>
      <td>spoon</td>
      <td>bowl</td>
      <td>banana</td>
      <td>apple</td>
    </tr>
    <tr>
      <td>sandwich</td>
      <td>orange</td>
      <td>broccoli</td>
      <td>carrot</td>
      <td>hot dog</td>
      <td>pizza</td>
      <td>donut</td>
      <td>cake</td>
    </tr>
    <tr>
      <td>chair</td>
      <td>sofa</td>
      <td>pottedplant</td>
      <td>bed</td>
      <td>diningtable</td>
      <td>toilet</td>
      <td>tvmonitor</td>
      <td>laptop</td>
    </tr>
    <tr>
      <td>mouse</td>
      <td>remote</td>
      <td>keyboard</td>
      <td>cell phone</td>
      <td>microwave</td>
      <td>oven</td>
      <td>toaster</td>
      <td>sink</td>
    </tr>
    <tr>
      <td>refrigerator</td>
      <td>book</td>
      <td>clock</td>
      <td>vase</td>
      <td>scissors</td>
      <td>teddy bear</td>
      <td>hair drier</td>
      <td>toothbrush</td>
    </tr>
  </table>
</details>

- Build the scene and run the APK on your headset. Look around your room and see how tracked objects receive a bounding box in accurate 3D space.

## 3. **[QR Code Tracking](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#-qr-code-tracking-with-zxing)**
- Open the `QRCodeTracking` scene to test real-time QR code detection and tracking.
- Install [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity)
- Click on the `NuGet` menu and then on `Manage NuGet Packages`. Search for the [ZXing.Net package](https://github.com/micjahn/ZXing.Net/) from Michael Jahn and install it.
- Make sure in your `Player Settings` under `Scripting Define Symbols` you see `ZXING_ENABLED`. The ZXingDefineSymbolChecker class should automatically detect if `ZXing.Net` is installed and add the symbol.
- In order to see the label of your QR code, you will also need to install TextMeshPro!
- Build the scene and run the APK on your headset. Look at a QR code to see the marker in 3D space and URL of the QR code.

## 4. **[Shader Samples](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#shader-samples)**
- Open the `Shader Samples` scene.
- Build the scene and run the APK on your headset.
- Look at the spheres from different angles and observe how objects behind it are changing.
- You can grab the examples too and move them around.

> [!WARNING]  
> The Meta Project Setup Tool (PST) will show a warning (opaque textures) and tell you to uncheck it, so do not fix this warning.

## 5. **[OpenAI vision model & voice commands](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#-openai-vision-model)**
- Open the `ImageLLM` scene.
- Make sure to create an [API key](https://platform.openai.com/api-keys) and enter it in the `OpenAI Manager prefab`.
- Select your desired model and optionally give the LLM some instructions.
- Make sure your headset is connected to the internet (the faster the better).
- Build the scene and run the APK on your headset.

> [!NOTE]  
> File uploads are currently limited to `25 MB` and the following input file types are supported: `mp3`, `mp4`, `mpeg`, `mpga`, `m4a`, `wav`, and `webm`.

You can send commands and receive results in any of these languages:
<details>
  <summary>Show all suppported languages</summary>
<table>
  <tr>
    <td>Afrikaans</td>
    <td>Arabic</td>
    <td>Armenian</td>
    <td>Azerbaijani</td>
    <td>Belarusian</td>
    <td>Bosnian</td>
    <td>Bulgarian</td>
    <td>Catalan</td>
    <td>Chinese</td>
  </tr>
  <tr>
    <td>Croatian</td>
    <td>Czech</td>
    <td>Danish</td>
    <td>Dutch</td>
    <td>English</td>
    <td>Estonian</td>
    <td>Finnish</td>
    <td>French</td>
    <td>Galician</td>
  </tr>
  <tr>
    <td>German</td>
    <td>Greek</td>
    <td>Hebrew</td>
    <td>Hindi</td>
    <td>Hungarian</td>
    <td>Icelandic</td>
    <td>Indonesian</td>
    <td>Italian</td>
    <td>Japanese</td>
  </tr>
  <tr>
    <td>Kannada</td>
    <td>Kazakh</td>
    <td>Korean</td>
    <td>Latvian</td>
    <td>Lithuanian</td>
    <td>Macedonian</td>
    <td>Malay</td>
    <td>Marathi</td>
    <td>Maori</td>
  </tr>
  <tr>
    <td>Nepali</td>
    <td>Norwegian</td>
    <td>Persian</td>
    <td>Polish</td>
    <td>Portuguese</td>
    <td>Romanian</td>
    <td>Russian</td>
    <td>Serbian</td>
    <td>Slovak</td>
  </tr>
  <tr>
    <td>Slovenian</td>
    <td>Spanish</td>
    <td>Swahili</td>
    <td>Swedish</td>
    <td>Tagalog</td>
    <td>Tamil</td>
    <td>Thai</td>
    <td>Turkish</td>
    <td>Ukrainian</td>
  </tr>
  <tr>
    <td>Urdu</td>
    <td>Vietnamese</td>
    <td>Welsh</td>
    <td></td>
    <td></td>
    <td></td>
    <td></td>
    <td></td>
    <td></td>
  </tr>
</table>
</details>

## 6. **[WebRTC video streaming](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#-webrtc-video-streaming)**

- Open the `Package Manager`, click on the + sign in the upper left/right corner.
	- Select "Add package from git URL".
	- Enter URL and click on Install:
	```
	https://github.com/endel/NativeWebSocket.git#upm
	```	
	- After the installation finished, click on the + sign in the upper left/right corner again.
	- Enter URL and click on Install:
	```
	https://github.com/Unity-Technologies/com.unity.webrtc.git
	```
	- After the installation finished, click on the + sign in the upper left/right corner again.
	- Enter URL and click on Install:
	```
	https://github.com/FireDragonGameStudio/SimpleWebRTC.git?path=/Assets/SimpleWebRTC
	```
- Open the `WebRTC-Quest` scene.
- Link up your signaling server on the `Client-STUNConnection` component in the `Web Socket Server Address` field.
- Build and deploy the `WebRTC-Quest` scene to your Quest3 device.
- Open the `WebRTC-SingleClient` scene on your Editor.
- Build and deploy the `WebRTC-SingleClient` scene to another device or start it from within the Unity Editor. More information can be found [here](https://www.youtube.com/watch?v=-CwJTgt_Z3M)
- Start the WebRTC app on your Quest and on your other devices. Quest and client streaming devices should connect automatically to the websocket signaling server.
- Perform the Start gesture with your left hand, or press the menu button on your left controller to start streaming from Quest3 to your WebRTC client app.

**Troubleshooting**:
- If there are compiler errors, make sure all packages were imported correctly.
	- Open the `Package Manager`, click on the + sign in the upper left/right corner.
	- Select "Add package from git URL".
	- Enter URL and click on Install:
	```
	https://github.com/endel/NativeWebSocket.git#upm
	```
	- After the installation finished, click on the + sign in the upper left/right corner again.
	- Enter URL and click on Install:
	```
	https://github.com/Unity-Technologies/com.unity.webrtc.git
	```
	- After the installation finished, click on the + sign in the upper left/right corner again.
	- Enter URL and click on Install:
	```
	https://github.com/FireDragonGameStudio/SimpleWebRTC.git?path=/Assets/SimpleWebRTC
	```
	- Use the menu `Tools/Update WebRTC Define Symbol` to update the scripting define symbols if needed.
- Make sure your own websocket signaling server is up and running. You can find more information about the necessary steps [here](https://youtu.be/-CwJTgt_Z3M?t=1458).
- If you're going to stream over LAN, make sure the `STUN Server Address` field on `[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor/Client-STUNConnection` is empty, otherwise leave the default value.
- Make sure to enable the `Web Socket Connection active` flag on `[BuildingBlock] Camera Rig/TrackingSpace/CenterEyeAnchor/Client-STUNConnection` to connect to the websocket server automatically on start.
- WebRTC video streaming **DOES** work, when the **Graphics API** is set to **Vulkan** (as of Unity WebRTC version 3.0.0). It works with **OPENGLES3** too. Make sure to use the right **Graphics API** for your project under `Project Settings/Player`.
	- When using **OPENGLES3** and **Oculus**: Make sure to **DISABLE** the **Low Overhead Mode (GLES)** setting for Android in `Project Settings/XR Plug-In Management/Oculus`. Otherwise this optimization will prevent your Quest from sending the video stream to a receiving client.
	- When using **OPENGLES3** and **OpenXR**: Make sure to **DISABLE** the **Meta Quest: Occlusion** and **Meta XR Subsampled Layout** settings for OpenXR in `Project Settings/XR Plug-In Management/OpenXR`. Otherwise this will prevent your Quest app from building, as these features are not supported by OpenGL.

> [!WARNING] 
> The Meta Project Setup Tool (PST) will show 3 warnings (opaque textures, low overhead mode GLES (-> only when working with **OPENGLES3** and the Oculus package) and camera stack). Do NOT fix this warnings.

## 7. **[Experimental QRCode detection](https://github.com/xrdevrob/QuestCameraKit?tab=readme-ov-file#-experimental-qrcode-detection)**

- Make sure TextMeshPro essentials are installed.
- Enable experimental mode on your Quest3. You can find more information about the necessary steps [here](https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-qrcode-detection).
- Build and deploy the `QRCodeDetection` scene to your Quest3 device.
- Use your controllers to interact with the UI. No hand tracking support yet.

**Troubleshooting**:
- Go to `Window/TextMeshPro` and click on `Import TMP Essential Resources`
- Make sure experimental mode is active. You can find more information about the necessary steps [here](https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-qrcode-detection).

# General Troubleshooting & Known Issues

- Some users have reported that the app crashes the second and every following time the app is opened. A solution described was to go to the Quest settings under `Privacy & Security` and toggle the camera permission and then start the app and accept the permission again. If you encounter this problem please open an issue and send me the crash logs. Thank you!
- If switching betwenn Unity 6 and other versions such as 2023 or 2022 it can happen that your Android Manifest is getting modified and the app won't run anymore. Should this happen to you make sure to go to `Meta > Tools > Update AndroidManifest.xml` or `Meta > Tools > Create store-compatible AndroidManifest.xml`. After that make sure you add back the `horizonos.permission.HEADSET_CAMERA` manually into your manifest file.

# Acknowledgements & Credits

- Thanks to **Meta** for the Passthrough Camera API and [**Passthrough Camera API Samples**](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples/).
- Thanks to shader wizard [Daniel Ilett](https://www.youtube.com/@danielilett) for helping me in the shader samples.
- Thanks to **[Michael Jahn](https://github.com/micjahn/ZXing.Net/)** for the XZing.Net library used for the QR code tracking samples.
- Thanks to **[Julian Triveri](https://github.com/trev3d/QuestDisplayAccessDemo)** for constantly pushing the boundaries with what is possible with Meta Quest hardware and software.
- Special thanks to [Markus Altenhofer](https://www.linkedin.com/in/markus-altenhofer-176453155/) from [FireDragonGameStudio](https://www.youtube.com/@firedragongamestudio) for contributing the WebRTC sample scene.
- Special thanks to [Thomas Ratliff](https://x.com/devtom7) for contributing his [shader samples](https://x.com/devtom7/status/1902033672041091453) to the repo.

# Community Highlights

I wanted to dedicate this section to a couple of developers that are doing some great work with Meta Quest and especially the Passthrough Camera API and are actively working on some exciting prototypes:

- [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): Takashi has been doing amazing explorations in XR and with Meta Quest for a long time. His work focuses mainly on scene understanding and scanning. Takashi has also created his own [GitHub repo](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking), showing other developers how to use the Unity OpenCV plugin for marker tracking and more.
  - [Turning image into colored point cloud](https://x.com/Tks_Yoshinaga/status/1900923909962133782)
  - [Point cloud data from Quest in real time](https://x.com/taka_yoshinaga/status/1910141763173712333)
  - [Map color images onto a point cloud by combining Quest's Depth API and Passthrough Camera API](https://x.com/Tks_Yoshinaga/status/1916381129508012107)
  - [Using Passthrough Camera API with the OpenCV for Unity plugin](https://x.com/Tks_Yoshinaga/status/1901187442084098464)
  - [OpenCV marker detection for object tracking](https://x.com/taka_yoshinaga/status/1901560686603387255)
  - [OpenCV marker detection for multiple objects](https://x.com/Taka_Yoshinaga/status/1902700309933371558)

- [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): Christoph was one of the first developers to publicly jump on the the Passthrough Camera API and post his prototypes. He is working on a very interesting and innovative football tracker project. This project uses the Camera2 API because it is slightly faster and less performance-demanding as oppose to Unity's WebCamTexture implementation. The football is tracked according to its color. Color tracking is handled via OpenCV for Unity. A color picker allows the user to directly select the color to be tracked from the object itself. Check out Christoph's work:
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [Tracking a real ball and playing some XR football](https://www.linkedin.com/feed/update/urn:li:activity:7314282273791471616/)
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [Custom ball-controller for the Meta Quest 3](https://www.linkedin.com/posts/christoph-spinger-280621190_i-just-worked-up-a-real-sweat-playing-vr-activity-7321457451889844225-HiTv?utm_source=share&utm_medium=member_desktop&rcm=ACoAACRDIgYBe94CQK8Ln4nJhdS1WdG2y9aZHYs)
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [Chameleon color picker](https://www.linkedin.com/feed/update/urn:li:activity:7306688023843250176/)
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [QR code object tracking](https://www.linkedin.com/feed/update/urn:li:activity:7306652200418598912/)

- [Thomas Ratcliff](https://x.com/devtom7): A super talented technical artist you should definitely follow. He has been working on passthrough shaders and lighting estimation.
  - [Water, blur, zoom shaders](https://x.com/devtom7/status/1901384363658350612)
  - [Pixelate shader](https://x.com/devtom7/status/1902033672041091453)
  - [Dynamic reflection data generation](https://x.com/devtom7/status/1913667214399414659)
  - [Camera based reflection and lighting](https://x.com/devtom7/status/1913965819987366267)
  
- [Hugues Bruyère](https://x.com/smallfly): Hugues is showcasing and continuously improving his image capturing and stable diffusion prototype. It demonstartes how to capture multiple images, geenrate new images, and stitch them together.
  - [MR + Diffusion prototype](https://x.com/smallfly/status/1901403937321750862)
  - [SAM 2 to our workflow to segment people](https://x.com/smallfly/status/1903560186381377735)
  - [Mixed Reality + Diffusion prototype as a tool for exploring concepts, styles, and moods by transforming real-world surroundings into alternate realities.](https://x.com/smallfly/status/1916234097724215599)

- [Markus Altenhofer](https://www.linkedin.com/in/markus-altenhofer-176453155/): Markus has been putting out some amazing content around Meta Quest and recently combining it with the Passthrough Camera API. You can find all his work on his YouTube channel [FireDragonGameStudio](https://www.youtube.com/@firedragongamestudio).
  - [Easy WebRTC video streaming to multiple devices](https://www.youtube.com/watch?v=1R9yrXePJ40)
  - [Indoor Navigation with QRCode tracking](https://www.youtube.com/watch?v=EUqaOGJxLiY&t)
  - [XR Minecraft with Meta Quest 3 Depth Sensor and ColorPicker](https://www.youtube.com/watch?v=CNoueOloXNo&t)
  - [Room detection with Quest 3 Depth Sensor and Object Detection](https://www.youtube.com/watch?v=iimtkRqRxLc)

# Community Contributions

- **Tutorials**
  - **XR Dev Rob - XR AI Tutorials**, [Watch on YouTube](https://www.youtube.com/watch?v=1z3pcMJbnRA)
  - **Dilmer Valecillos**, [Watch on YouTube](https://www.youtube.com/watch?v=lhnuP6lJ_yY)
  - **Skarredghost**, [Watch on YouTube](https://www.youtube.com/watch?v=A2ZhJt-SIBU)
  - **FireDragonGameStudio**, [Watch on YouTube](https://www.youtube.com/watch?v=1R9yrXePJ40)
  - **xr masiso**, [Watch on YouTube](https://www.youtube.com/watch?v=FXFgkAmvpgo)
  - **Urals Technologies**, [Watch on YouTube](https://www.youtube.com/playlist?list=PLU7W-ZU9OIiEanYEKtjyHQIoLrf0SflXx)

- **Object Detection**
  - [Udayshankar Ravikumar](https://x.com/uralstechCTO): [Unity Sentis Digit Recognition](https://x.com/uralstechCTO/status/1902056175153377353)
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [Tracking a real ball and playing some XR football](https://www.linkedin.com/feed/update/urn:li:activity:7314282273791471616/)
  - [Danyl Bulbas](https://www.linkedin.com/in/danylbulbas/): [XR Chess](https://www.linkedin.com/feed/update/urn:li:activity:7313374042441375745/)
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [Custom ball-controller for the Meta Quest 3](https://www.linkedin.com/posts/christoph-spinger-280621190_i-just-worked-up-a-real-sweat-playing-vr-activity-7321457451889844225-HiTv?utm_source=share&utm_medium=member_desktop&rcm=ACoAACRDIgYBe94CQK8Ln4nJhdS1WdG2y9aZHYs)

- **Shaders**
  - [Thomas Ratcliff](https://x.com/devtom7): [Water, blur, zoom shaders](https://x.com/devtom7/status/1901384363658350612)
  - [Thomas Ratcliff](https://x.com/devtom7): [Pixelate shader](https://x.com/devtom7/status/1902033672041091453)
  - [Chukwfumnanya Christoph-Antoine Okafor](https://x.com/covetthatjam): [Frosted Glass Shader](https://x.com/covetthatjam/status/1902423661102923999)
  - [Bastion](https://x.com/BastionReality): [Glitch Shader](https://x.com/BastionReality)

- **Environment Understanding & Mapping**
  - [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): [Turning image into colored point cloud](https://x.com/Tks_Yoshinaga/status/1900923909962133782)
  - [Alireza Bahremand](https://x.com/lirezaBahremand): [Quest Passthrough to MAST3R-SLAM for scene ply distribution](https://x.com/lirezaBahremand/status/1901665472069902772)
  - [うえださん](https://x.com/ueda406): [3D Scanner](https://x.com/ueda406/status/1904771581135774042)
  - [Sander Sneek](https://www.linkedin.com/in/sandersneek/): [Mixed Reality Voxel Demo](https://www.linkedin.com/feed/update/urn:li:activity:7312869115583414273/)
  - [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): [Point cloud data from Quest in real time](https://x.com/taka_yoshinaga/status/1910141763173712333)
  - [Bastion](https://x.com/BastionReality): [Sobel edge detection + passthrough camera + R6 Lion scan SFX](https://x.com/BastionReality/status/1912358908804333844)
  - [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): [Map color images onto a point cloud by combining Quest's Depth API and Passthrough Camera API](https://x.com/Tks_Yoshinaga/status/1916381129508012107)

- **Lighting and Reflection Estimation**
  - [pjchardt on Reddit](https://www.reddit.com/user/pjchardt/): [Prototype combining real lights and virtual objects using light estimation to affect 3d environment.](https://www.reddit.com/r/OculusQuest/comments/1jlvy3o/meta_quest_cameraapi_prototype_combining_real/)
  - [Thomas Ratcliff](https://x.com/devtom7): [Dynamic reflection data generation](https://x.com/devtom7/status/1913667214399414659)
  - [Thomas Ratcliff](https://x.com/devtom7): [Camera based reflection and lighting](https://x.com/devtom7/status/1913965819987366267)

- **Environment Sampling**
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [Chameleon color picker](https://www.linkedin.com/feed/update/urn:li:activity:7306688023843250176/)
  - [Sid Naik](https://www.linkedin.com/in/sidharthrnaik/): [Copy and paste the lighting in his house](https://www.linkedin.com/posts/sidharthrnaik_augmentedreality-virtualreality-vr-activity-7307523483318566912-NpGr/?utm_source=share&utm_medium=member_ios&rcm=ACoAACRDIgYBe94CQK8Ln4nJhdS1WdG2y9aZHYs)
  - [Markus Altenhofer](https://www.linkedin.com/in/markus-altenhofer-176453155/): [XR Minecraft with Meta Quest 3 Depth Sensor and ColorPicker](https://www.youtube.com/watch?v=CNoueOloXNo)

- **Image to 3D**
  - [Takahiro Horikawa](https://x.com/thorikawa): [Replicate real objects into 3D](https://x.com/thorikawa/status/1901545245944455409)
  - [Matt Hudson](https://x.com/mechpil0t): [Augmented Reality clones](https://x.com/mechpil0t/status/1902879317803294773)
  - [Aman Bohra](https://www.linkedin.com/in/amanbohra/): [Turning drawings into 3D models](https://www.linkedin.com/feed/update/urn:li:activity:7310875897278918656/)

- **Image to Image, Diffusion & Generation**
  - [Hugues Bruyère](https://x.com/smallfly): [MR + Diffusion prototype](https://x.com/smallfly/status/1901403937321750862)
  - [Hugues Bruyère](https://x.com/smallfly): [SAM 2 to our workflow to segment people](https://x.com/smallfly/status/1903560186381377735)
  - [水マヨ](https://x.com/mizzmayo): [AI image description](https://x.com/mizzmayo/status/1901855438083359120)
  - [妹尾雄大](https://x.com/senooyudai): [Img2Img process of SDXL](https://x.com/senooyudai/status/1900799052054491421)
  - [Rolando Masís-Obando](https://www.linkedin.com/in/rmasiso): [Image to image with LCM and SDXL Turbo](https://www.linkedin.com/feed/update/urn:li:activity:7307797312158789632/)
  - [Hugues Bruyère](https://x.com/smallfly): [Mixed Reality + Diffusion prototype as a tool for exploring concepts, styles, and moods by transforming real-world surroundings into alternate realities.](https://x.com/smallfly/status/1916234097724215599)
 
- Video recording and replay
  - [Lucas Martinic](https://x.com/lucas_martinic): [Rewind what you saw](https://x.com/lucas_martinic/status/1902700951728693618)

- **OpenCV for Unity**
  - [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): [Using Passthrough Camera API with the OpenCV for Unity plugin](https://x.com/Tks_Yoshinaga/status/1901187442084098464)
  - [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): [OpenCV marker detection for object tracking](https://x.com/taka_yoshinaga/status/1901560686603387255)
  - [Takashi Yoshinaga](https://x.com/Taka_Yoshinaga): [OpenCV marker detection for multiple objects](https://x.com/Taka_Yoshinaga/status/1902700309933371558). You can find this project on his [GitHub Repo](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking)
  - [Aurelio Puerta Martín](https://x.com/aurepuerta_dev): [OpenCV with multiple trackers](https://x.com/aurepuerta_dev/status/1905220037167751675)
  - [くりやま@システム開発](https://x.com/xamel7): [Positioning 3D objects on markers](https://x.com/xamel7/status/1904400697577394181)

- **QR Code Tracking**
  - [Christoph Spinger](https://www.linkedin.com/in/christoph-spinger-280621190/): [QR code object tracking](https://www.linkedin.com/feed/update/urn:li:activity:7306652200418598912/)
 
# News

- (Mar 21 2025) The Mysticle - [One of Quests Most Exciting Updates is Now Here!](https://www.youtube.com/watch?v=dG1rxxVDb9Y)
- (Mar 18 2025) Road to VR - [Meta Releases Quest Camera Access for Developers, Promising Even More Immersive MR Games](https://www.roadtovr.com/meta-releases-quest-camera-access-for-developers-promising-even-more-immersive-mixed-reality-games/)
- (Mar 17 2025) MIXED Reality News - [Quest developers get new powerful API for mixed reality apps](https://mixed-news.com/en/meta-quest-3-passthrough-camera-api-experimental-release/)
- (Mar 14 2025) UploadVR - [Quest's Passthrough Camera API Is Out Now, Though Store Apps Can't Yet Use It](https://www.uploadvr.com/quest-passthrough-camera-api-experimental-out-now/)
 
# License

This project is licensed under the MIT License. See the LICENSE file for details. Feel free to use the samples for your own projects, though I would appreciate if you would leave some credits to this repo in your work ❤️

# Contact

For questions, suggestions, or feedback, please open an issue in the repository or contact me on [X](https://x.com/xrdevrob), [LinkedIn](https://www.linkedin.com/in/robertocoviello/), or at [roberto@blackwhale.dev](mailto:roberto@blackwhale.dev). Find all my info [here](https://bento.me/xrdevrob) or join our growing XR developer community on [Discord](https://discord.gg/KkstGGwueN).

[![Sponsor](https://img.shields.io/badge/Sponsor-❤️-FF69B4.svg)](https://github.com/sponsors/xrdevrob)
[![Support on Patreon](https://img.shields.io/badge/Become%20a%20Patron-orange?logo=patreon&style=flat-square)](https://www.patreon.com/c/blackwhalestudio)

--------------------------------------------------------------------------------
Happy coding and enjoy exploring the possibilities with QuestCameraKit!

--------------------------------------------------------------------------------
