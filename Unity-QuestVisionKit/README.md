# Unity-QuestVisionKit

Quest passthrough camera samples for Meta Quest, focused on practical vision workflows in Unity.

## Current Sample Scenes

1. `Assets/Samples/1 ColorPicker/ColorPicker.unity`  
   Map world-space ray hits to camera pixels and sample real-world color.
2. `Assets/Samples/2 ObjectDetection/ObjectDetection.unity`  
   On-device object detection using Unity AI Inference (`com.unity.ai.inference`).
3. `Assets/Samples/3 QRCodeTracking/QRCodeTracking.unity`  
   QR tracking flow using ZXing.
4. `Assets/Samples/4 Shaders/CameraMappingForShaders.unity`  
   Stereo passthrough shader mapping sample (camera mapping, frosted glass, wavy portal).
5. `Assets/Samples/5 ImageLLM/ImageLLM.unity`  
   Voice -> capture passthrough frame -> OpenAI vision response -> optional speech output.
6. `Assets/Samples/6 WebRTC/WebRTC-Quest.unity` and `Assets/Samples/6 WebRTC/WebRTC-SingleClient.unity`  
   WebRTC passthrough streaming setup.
7. `Assets/Samples/7 QRCodeDetection/QRCodeDetection.unity`  
   Experimental MRUK-based QR code detection.

## What Changed (Latest)

- Shader + camera-mapping content has been consolidated under `Assets/Samples/4 Shaders/`.
- Legacy `Assets/Samples/CameraMapping/` and older shader sample scene variants were removed.
- New consolidated scene entry point: `Assets/Samples/4 Shaders/CameraMappingForShaders.unity`.

## Requirements

- Unity project with Meta Quest XR setup.
- Key packages in this repo:
  - `com.meta.xr.sdk.core`
  - `com.meta.xr.mrutilitykit`
  - `com.unity.ai.inference` (`2.3.0`)
  - `com.unity.xr.openxr`
  - `com.unity.xr.meta-openxr`

Optional per sample:

- QRCodeTracking: ZXing package/dll.
- WebRTC sample: NativeWebSocket + Unity WebRTC + SimpleWebRTC.
- ImageLLM: OpenAI API key.

## Quick Start

1. Open one of the sample scenes listed above.
2. Confirm Quest/OpenXR project setup is valid.
3. Build to Quest and run.

## Notes

- `ProjectSettings/EditorBuildSettings.asset` currently has an empty `m_Scenes` list. Add scenes to Build Settings before building from the editor.
- Keep API keys out of prefabs/scenes; use local-only config where possible.
