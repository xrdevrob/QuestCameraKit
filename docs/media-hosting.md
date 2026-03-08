# README Media Hosting

This repository keeps README showcase media out of Git LFS whenever possible.
The goal is simple: users should be able to clone the project without pulling
large demo GIFs or videos.

## Policy

- Keep small thumbnails and screenshots in the repository.
- Host heavy demo media externally.
- Do not add new README showcase GIFs or videos to Git LFS.

Good external hosts:

- GitHub release assets
- GitHub user attachments
- External object storage or CDN

## Recommended Pattern

Use a lightweight in-repo thumbnail that links to externally hosted media:

```md
[![Object Detection Demo](Media/ObjectDetection-thumb.png)](https://github.com/<owner>/<repo>/releases/download/media/object-detection.mp4)
```

If you need an inline image in the README, point directly at an external URL:

```md
![Color Picker Demo](https://github.com/<owner>/<repo>/releases/download/media/color-picker.gif)
```

## Migration Checklist

1. Upload the heavy media file to a GitHub release or another external host.
2. Add or keep a small thumbnail in `Media/`.
3. Update `README.md` to reference the external asset.
4. Do not commit the large GIF/video into the repository tree.

## Important Note

Changing `.gitattributes` only affects new tracking behavior. Existing LFS
objects already in history stay in LFS until they are migrated or replaced.
