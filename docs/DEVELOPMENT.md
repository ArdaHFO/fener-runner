# Development guide

[English](DEVELOPMENT.md) · [Türkçe](DEVELOPMENT.tr.md) · [Back to the project](../README.md)

## Toolchain

| Component | Version or requirement |
| --- | --- |
| Unity Editor | 6000.3.0f1 |
| Universal Render Pipeline | 17.3.0 |
| Unity UI | 2.0.0 |
| Friends | 1.3.0 |
| Multiplayer Services | 1.2.1 |
| Unity Transport | 2.6.0 |
| Android build modules | Android Build Support, SDK & NDK Tools, OpenJDK for this Editor |

Package versions are pinned in `UnityProject/Packages`. Keep `manifest.json` and `packages-lock.json` together when updating dependencies. The verified development environment is Windows. Portability to another host platform has not been validated.

## Open and play

```powershell
git clone https://github.com/ArdaHFO/fener-runner.git
cd fener-runner
.\scripts\open-unity.ps1 -UnityEditor "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe"
```

Alternatively, add `UnityProject` through Unity Hub. The Windows launcher uses `-force-d3d11` for the Editor. This does not change the Android graphics configuration.

The first import recreates `Library` and downloads packages. Open `Assets/FenerRunner/Generated/Scenes/FenerRunner.unity`, enter Play Mode, then choose **SAHAYA ÇIK**. Arrow keys provide Editor movement controls. Keep the supplied generated scene, prefabs and their `.meta` files together.

## Code and asset map

Paths below are relative to `UnityProject/Assets/FenerRunner`.

| Location | Purpose |
| --- | --- |
| `Runtime/RunnerGame.cs` | Run lifecycle and coordination |
| `Runtime/RunnerMotor.cs` | Lane movement, jump and slide motion |
| `Runtime/RunnerPowerups.cs` | Power definitions and active effects |
| `Runtime/RunWallet.cs` | Currency, upgrades and persistent progression |
| `Runtime/Multiplayer/` | Friends, invitations, Relay, ghost transport and UI |
| `Runtime/Development/` | Editor-only gameplay verification harnesses |
| `Editor/` | Scene and asset tooling, build entry points and regression orchestration |
| `Generated/` | Authored/generated game scene and runtime assets |
| `Art/`, `Resources/` | Character, environment, audio and runtime-loaded assets |

The ready-to-open scene is included. Historical asset generators describe earlier development steps and should not be treated as an interchangeable full rebuild pipeline. Use focused tools only after reviewing their effects.

## Build Android

Install the Android modules for the pinned Editor first. Stop Play Mode before building.

From Unity, use **Fener Runner → 07 - Build Android APK (ARM64)**. The command switches the active target when needed, then builds the supplied scene.

From PowerShell:

```powershell
.\scripts\build-android.ps1 -UnityEditor "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe"
```

Use `-OutputPath` to choose another APK path. The underlying Unity entry point is:

```text
-batchmode -quit -buildTarget Android
-executeMethod FenerRunner.Editor.AndroidApkBuilder.Build
-apkOutput <absolute-path-to-output.apk>
```

The builder configures ARM64, IL2CPP Release, OpenGLES3, ETC2, Android API 26 minimum, portrait orientation and a non-development APK. It excludes Editor verification components from the built scene and writes a build report under `Validation` next to `UnityProject`.

The public project uses local debug signing. The maintainer's signing key is not included. Your locally compiled APK will generally have a different signer from the downloadable preview and cannot replace it as an in-place update. The exact binary in the GitHub release is the previously verified v0.20 build, not a promise of byte-identical reproduction on another machine.

For a new release, update the version and version code in `Editor/AndroidApkBuilder.cs`, review package settings, and rerun the relevant validation. Keep signing material and credentials outside the repository.

## Validate changes

Use Play Mode to exercise the behavior you changed before expanding to a broader regression run. Important scenarios include late jumps/slides, lane-change collection, power expiry, pause/resume, returning to the menu, purchase persistence and multiplayer disconnects.

The recorded v0.20 regression baseline passed **27 native Play Mode stages and 2,993 conditions**. Full-suite orchestration lives in `Editor/VersionTwentyValidation.cs`. The Editor menu is **Fener Runner → 00 - Verify workshop and friends v20 without APK**. The command-line entry `FenerRunner.Editor.VersionTwentyValidation.ReviewAndExit` exits when asynchronous orchestration finishes, so it must not be combined with `-quit`.

These harnesses require native Editor graphics. They are not Unity Test Framework tests, and the complete suite includes live Friends and Relay operations using test identities. Run cloud checks deliberately against a configured development project. Do not assume a headless or offline run provides the same coverage. See [multiplayer development](MULTIPLAYER.md#developing-online-features).

[Validation documentation](VALIDATION.md) records the baseline, test boundaries and remaining device checks. A historical pass does not automatically validate subsequent source changes or a freshly built APK.

## Repository hygiene

Commit source, scene assets, `.meta` files, package locks and intended documentation. Keep Unity caches, local Editor state, logs, build outputs, signing keys and service credentials untracked. Release APKs belong in GitHub Releases rather than the source tree.

Preserve asset GUIDs when moving files. Make focused commits and describe both the observable change and its verification. Review [third-party notices](../THIRD_PARTY_NOTICES.md) before adding or redistributing external assets.
