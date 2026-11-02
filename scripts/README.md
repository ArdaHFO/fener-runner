# Developer helpers / Geliştirici araçları

These Windows helpers require PowerShell 5.1 or newer and Unity **6000.3.0f1**. Paths are resolved from the checkout, so they can be called from any working directory. They never install Unity, publish an APK, or change permanent system environment settings.

```powershell
# Inspect the command without starting Unity or creating files.
.\scripts\open-unity.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe' -WhatIf

# Open the project with the Windows Direct3D 11 renderer.
.\scripts\open-unity.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe'

# Close the project's Editor before building.
.\scripts\build-android.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe' -WhatIf
.\scripts\build-android.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe'

# Optional output and short, writable build-cache location.
.\scripts\build-android.ps1 -UnityEditor 'C:\Unity\Editor\Unity.exe' -OutputPath 'Builds\Android\MyPreview.apk' -BuildCacheRoot 'C:\fr-build'

# Source/package/document integrity only. Does not start Unity.
python .\scripts\verify-repository.py
```

Install Android Build Support, Android SDK/NDK Tools and OpenJDK through Unity Hub before building. The default output is `Builds/Android/FenerRunner-v0.20-arm64.apk`. The default cache is `.local-build/`, with the build log, Gradle cache, Java temporary files and `android-user/` signing state inside it. Long checkout paths need the optional `-BuildCacheRoot` argument because Java worker sockets have a short path limit. Use a directory you can write to, and use the same directory for subsequent builds.

**Keep your local signing key.** The first build creates local debug signing state in the selected cache's `android-user/` directory. The repository does not contain the original APK's signing key. A fresh local key cannot update the downloadable APK in place. Changing cache locations can create a different key. For independently distributed builds, choose your own package identifier and signing setup. The helper creates a non-development preview, not a store-signed release.

`verify-repository.py` uses only Python's standard library. It checks required project files, pinned package/lock agreement, Unity `.meta` pairing and unique GUIDs, local documentation links, and repository hygiene. It does **not** compile C#, resolve Unity package assets, exercise gameplay, or test Android hardware. The GitHub Actions workflow runs this same check with read-only repository permission.

## Türkçe

Unity **6000.3.0f1** ve PowerShell 5.1+ gerekir. `-UnityEditor` kurulu `Unity.exe` dosyasını göstermelidir. `-WhatIf`, Unity'yi başlatmadan ve dosya oluşturmadan komutu gösterir. `open-unity.ps1` projeyi açar. `build-android.ps1`, Editor kapalıyken Android önizleme APK'sını oluşturur, yayımlamaz. Android modüllerini önce Unity Hub'dan kurun.

Derleme çıktısı varsayılan olarak `Builds/Android/` altındadır. Uzun klasör yollarında `-BuildCacheRoot` ile kısa ve yazılabilir bir klasör seçin. Bu klasörün `android-user/` altındaki yerel imza anahtarını koruyun. Repo, indirilebilir APK'nın asıl imza anahtarını içermez. Yeni bir anahtarla derlediğiniz APK mevcut indirilebilir uygulamanın üzerine güncelleme olarak kurulamaz.

`python scripts/verify-repository.py`, kaynak dosya düzenini, Unity meta dosyalarını ve belge bağlantılarını kontrol eder. Unity derlemesi, oynanış testi veya gerçek telefon testi yerine geçmez.
