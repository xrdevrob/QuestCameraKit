# Contributing to QuestCameraKit

Thank you for considering contributing to QuestCameraKit! Your help makes this project even better and helps the AR/VR community explore the capabilities of Meta Quest’s Passthrough Camera API.

## How to Contribute

We welcome contributions in many forms:
- **Bug Reports:** If you find a bug, please open an issue with details.
- **Feature Requests:** Got an idea for a new sample or enhancement? Let us know!
- **Code Improvements:** Bug fixes, new features, or improvements to existing samples.
- **Documentation & Tutorials:** Enhancements to the README, sample instructions, or video tutorials.

## Reporting Bugs

If you encounter issues:
1. **Search existing issues** to see if the problem has been reported.
2. **Open a new issue:** Include a clear title, detailed description, and steps to reproduce the bug.
3. **Provide context:** Attach error logs, crash reports, or screenshots if available.

## Requesting Features

If you’d like to see new functionality:
- Open an issue with the label **"feature request"**.
- Describe your idea, its benefits, and how you envision it working.
- Feel free to share mockups or examples that help clarify your proposal.

## Pull Request Guidelines

When submitting a pull request:
1. **Fork the Repository:** Create your branch off of the `main` branch.
2. **Follow the Code Style:** Maintain clarity and consistency with the existing code. We put a lot of emphasis on making the samples simple and easy-to-understand, so other devs can build cool things quickly!
3. **Test Your Changes:** Ensure your modifications work on a supported Meta Quest device (Quest 3 or 3s with HorizonOS v74 or later) and in Unity (recommended Unity 6).
4. **Write Clear Commit Messages:** Describe what you’re changing and why.
5. **Link Related Issues:** Reference any issues or feature requests that your PR addresses.

Once your changes are ready, submit your pull request with a detailed explanation of the improvements and, if applicable, screenshots or demo videos.

## Local Development Setup

To set up QuestCameraKit on your machine:

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/xrdevrob/QuestCameraKit.git
   cd QuestCameraKit
   git lfs pull
   python3 scripts/check_repo.py
   ```

2. Open `Unity-QuestVisionKit` with the Unity version in `ProjectSettings/ProjectVersion.txt` and let Package Manager finish.
3. Run the checks and the selected sample build described in [Testing](docs/testing.md).
4. Keep credentials out of scenes and prefabs. Use a private development key only for local ImageLLM testing; production apps should call an authenticated backend.
