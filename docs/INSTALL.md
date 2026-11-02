# Install and play

[English](INSTALL.md) · [Türkçe](INSTALL.tr.md) · [Back to the project](../README.md)

## Download

Download [FenerRunner-v0.20-arm64.apk](https://github.com/ArdaHFO/Fener-Runner/releases/download/v0.20.0/FenerRunner-v0.20-arm64.apk) from the [v0.20.0 release](https://github.com/ArdaHFO/Fener-Runner/releases/tag/v0.20.0).

The preview requires **Android 8.0 or newer and a 64-bit ARM device**. The APK is approximately **34.8 MiB**, with no separate OBB download. It is not an iOS, Windows or browser installer.

## Install on your phone

1. Download the APK directly on the phone, or copy it from your computer.
2. Open the downloaded file using your browser's downloads screen or a file manager.
3. If Android asks, grant that app permission to install from this source. Follow the on-screen instructions to return to the installer.
4. Choose **Install**, or **Update** if a compatible version is already installed.
5. Open **Fener Runner** and select **SAHAYA ÇIK** to start a solo run.

This is a sideloaded development preview, not a Play Store release. The APK uses local debug-key signing even though Development Build is disabled. Android's installation messages vary by device.

## Updating an earlier version

The release uses package ID `com.fenerrunner.prototype`, version `0.20.0` and Android version code `20`. The recorded v0.19 and v0.12 builds use the same signing certificate.

Install over the existing application to preserve compatible local progress. Uninstalling or clearing application data can remove chicks, upgrades, the selected vehicle and access to the anonymous online identity. A separately compiled APK normally has a different signing key and may not install as an update to this download.

## Controls and menu labels

The current game interface is Turkish.

| Label or action | Meaning |
| --- | --- |
| **SAHAYA ÇIK** | Start a solo run |
| **TRİBÜN ATÖLYESİ** | Open the workshop |
| **GÜÇLER / YETENEKLER / GARAJ** | Powers / permanent abilities / vehicles |
| **ARKADAŞINLA KOŞ** | Open multiplayer |
| **ANA MENÜYE DÖN** | End the current run and return to the menu |
| Swipe left / right | Change lane |
| Swipe up, then up again | Jump, then double jump |
| Swipe down | Slide, including repeated slides |
| Speaker control | Adjust audio volume |

Chicks collected during a run become workshop currency. Power-ups activate when collected. Chase vehicles are cosmetic. The rare hen releases chicks onto the course, so move to collect the scattered rewards.

For online play, use the [friends and multiplayer guide](MULTIPLAYER.md). Both devices need an internet connection and the same game version.

## Verify the file

Expected size is **36,504,526 bytes**. Expected SHA-256:

```text
a8ced15d1ed41d80bb217e2947bffe469f84e63339c6f193ca882b3cd1b3c372
```

On Windows:

```powershell
Get-FileHash -Algorithm SHA256 .\FenerRunner-v0.20-arm64.apk
```

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Android cannot open or install the APK | Confirm Android 8.0+, ARM64 support, available storage and a complete download. Compare the file hash. |
| Update is rejected | A locally rebuilt or otherwise differently signed version may already be installed. Preserve any important app data before deciding how to replace it. |
| No sound | Check the in-game volume, Android media volume and connected Bluetooth audio output. |
| Friend cannot join | Both devices must use the same version. Check the connection and use a fresh invitation or room code. |
| Invitation does not arrive | Both apps must be open. Invitations expire after two minutes and do not generate background phone notifications. |

Physical-phone performance, Android keyboard behavior and two-phone play were not covered by the recorded release verification. If something fails, include the phone model, Android version, game version and exact steps in your report. See [validation scope](VALIDATION.md) and [privacy](PRIVACY.md).
