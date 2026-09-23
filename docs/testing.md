# Building and testing QuestCameraKit

Use **Unity 6000.3.12f1** with Android Build Support, its SDK/NDK and OpenJDK. Open `Unity-QuestVisionKit`, allow Package Manager to finish, and run `git lfs pull` if the clone contains unresolved assets. The packages are pinned; no separate sample package installation is required.

## Fast repository and Unity checks

From the repository root:

```sh
python3 scripts/check_repo.py
git diff --check
```

Run the executable regression checks with the Unity executable for your platform:

```sh
Unity -batchmode -nographics -quit \
  -projectPath /absolute/path/QuestCameraKit/Unity-QuestVisionKit \
  -buildTarget Android \
  -executeMethod QuestCameraKit.Editor.MaintenanceChecks.Run \
  -logFile /absolute/path/checks.log
```

These cover stereo PCM/WAV headers and samples, the bundled model's CPU output contract on a synthetic black input, command JSON escaping (including null and control characters), and native QR overlay transitions through absent/present/lost bounds. `MaintenanceChecks.CheckWavStereo` is the focused regression that failed before the stereo fix.

Run `QuestCameraKit.Editor.MaintenanceChecks.AuditScenes` the same way to inspect all seven sample scenes for missing MonoBehaviours. Its `scene-audit.txt` is written at the repository root. It opens scenes without saving them.

## Build one real sample for Quest

The regular **Build and Run** scene list includes all six headset samples, starting with ColorPicker and its sample menu. Y opens/closes the menu, left thumbstick selects, and X loads a scene. The desktop receiving peer is excluded. The CLI helper defaults to `AllSamples` for a combined APK; pass a specific scene name to build one sample as an Android ARM64 IL2CPP development APK:

```sh
Unity -batchmode -nographics -quit \
  -projectPath /absolute/path/QuestCameraKit/Unity-QuestVisionKit \
  -buildTarget Android \
  -executeMethod QuestCameraKit.Editor.SampleBuild.Build \
  -sampleScene ColorPicker \
  -apk /absolute/path/Builds/ColorPicker.apk \
  -logFile /absolute/path/build.log
```

Use `AllSamples` for the combined menu app, or `ObjectDetection`, `CameraMappingForShaders`, `ImageLLM`, `QRCodeDetection`, or `WebRTC-Quest` for the other headset samples. `WebRTC-SingleClient` is the receiving-peer scene. Development package IDs are `com.xrdevrob.questcamerakit.<lowercase-scene-name-without-hyphens>`; the helper restores the project's original application ID and product name afterward.

On the headset, keep the device awake and unlocked, grant camera and spatial-data permissions, and exercise the sample itself. Verify actual content with known objects and QR codes in view. Confirm color sampling, marker alignment during head motion, stereo shader appearance, and controller/hand interactions on hardware.

ImageLLM additionally needs a private API key, microphone permission and network access. Use only a development key for local testing; do not serialize production credentials into an APK. A production integration should send authenticated requests through your own backend. WebRTC needs a reachable signaling server and another receiving peer. Camera-startup tests do not establish end-to-end cloud or peer-to-peer success.
