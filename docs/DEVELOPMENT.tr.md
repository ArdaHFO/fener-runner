# Geliştirme rehberi

[English](DEVELOPMENT.md) · [Türkçe](DEVELOPMENT.tr.md) · [Projeye dön](../README.tr.md)

## Araçlar

| Bileşen | Sürüm veya gereksinim |
| --- | --- |
| Unity Editor | 6000.3.0f1 |
| Universal Render Pipeline | 17.3.0 |
| Unity UI | 2.0.0 |
| Friends | 1.3.0 |
| Multiplayer Services | 1.2.1 |
| Unity Transport | 2.6.0 |
| Android derleme modülleri | Bu Editor için Android Build Support, SDK & NDK Tools, OpenJDK |

Paket sürümleri `UnityProject/Packages` içinde sabitlenmiştir. Bağımlılıkları güncellerken `manifest.json` ve `packages-lock.json` dosyalarını birlikte tut. Doğrulanmış geliştirme ortamı Windows'tur. Başka işletim sistemlerinde geliştirme henüz doğrulanmadı.

## Aç ve oyna

```powershell
git clone https://github.com/ArdaHFO/fener-runner.git
cd fener-runner
.\scripts\open-unity.ps1 -UnityEditor "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe"
```

Alternatif olarak Unity Hub üzerinden `UnityProject` klasörünü ekle. Windows başlatıcısı Editor için `-force-d3d11` kullanır. Bu seçim Android grafik ayarını değiştirmez.

İlk açılışta `Library` yeniden oluşturulur ve paketler indirilir. `Assets/FenerRunner/Generated/Scenes/FenerRunner.unity` sahnesini aç, Play Mode'a gir ve **SAHAYA ÇIK** seç. Editor'de ok tuşları hareket kontrolü sağlar. Hazır sahne, prefab'lar ve `.meta` dosyaları birlikte korunmalıdır.

## Kod ve varlık yapısı

Aşağıdaki yollar `UnityProject/Assets/FenerRunner` klasörüne göredir.

| Konum | Görev |
| --- | --- |
| `Runtime/RunnerGame.cs` | Koşu yaşam döngüsü ve koordinasyon |
| `Runtime/RunnerMotor.cs` | Şerit değiştirme, zıplama ve kayma |
| `Runtime/RunnerPowerups.cs` | Güç tanımları ve etkin etkiler |
| `Runtime/RunWallet.cs` | Para, geliştirmeler ve kalıcı ilerleme |
| `Runtime/Multiplayer/` | Arkadaşlar, davetler, Relay, gölge iletişimi ve arayüz |
| `Runtime/Development/` | Yalnızca Editor'de çalışan oynanış doğrulama araçları |
| `Editor/` | Sahne ve varlık araçları, derleme girişleri ve regresyon yönetimi |
| `Generated/` | Hazır/üretilmiş oyun sahnesi ve çalışma zamanı varlıkları |
| `Art/`, `Resources/` | Karakter, ortam, ses ve çalışma anında yüklenen varlıklar |

Açılmaya hazır sahne repoya dahildir. Geçmiş varlık üreticileri önceki geliştirme adımlarını yansıtır ve birbirinin yerine kullanılabilen bir tam yeniden üretim süreci değildir. Belirli bir aracı çalıştırmadan önce etkisini incele.

## Android derle

Önce sabitlenmiş Editor sürümünün Android modüllerini kur. Derlemeden önce Play Mode'u durdur.

Unity içinde **Fener Runner → 07 - Build Android APK (ARM64)** seç. Komut gerektiğinde etkin hedefi değiştirir, ardından hazır sahneyi derler.

PowerShell üzerinden:

```powershell
.\scripts\build-android.ps1 -UnityEditor "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe"
```

Başka bir APK yolu seçmek için `-OutputPath` kullan. Alttaki Unity giriş noktası:

```text
-batchmode -quit -buildTarget Android
-executeMethod FenerRunner.Editor.AndroidApkBuilder.Build
-apkOutput <apk-icin-mutlak-dosya-yolu>
```

Derleyici ARM64, IL2CPP Release, OpenGLES3, ETC2, en düşük Android API 26, dikey ekran ve Development Build kapalı bir APK ayarlar. Editor doğrulama bileşenlerini derlenen sahneden çıkarır ve `UnityProject` yanındaki `Validation` klasörüne derleme raporu yazar.

Açık proje yerel debug imzasını kullanır. Geliştiricinin imza anahtarı repoya dahil değildir. Kendi bilgisayarında derlediğin APK'nın imzası genellikle indirilebilir önizlemeden farklı olur ve onun üzerine güncelleme olarak kurulamaz. GitHub sürümündeki dosya daha önce doğrulanmış v0.20 derlemesidir. Başka bir bilgisayarda bayt düzeyinde aynı dosyanın üretileceği iddia edilmez.

Yeni sürüm hazırlarken `Editor/AndroidApkBuilder.cs` içindeki sürümü ve sürüm kodunu güncelle, paket ayarlarını incele ve ilgili doğrulamaları yeniden çalıştır. İmza dosyalarını ve kimlik bilgilerini repo dışında tut.

## Değişiklikleri doğrula

Kapsamlı regresyona geçmeden önce değiştirdiğin davranışı Play Mode'da dene. Engele yakın zıplama/kayma, şerit değiştirirken toplama, güç süresinin bitmesi, duraklatma/devam, ana menüye dönme, satın almaların korunması ve multiplayer bağlantısının kesilmesi önemli senaryolardır.

Kayıtlı v0.20 temel doğrulamasında **27 native Play Mode aşaması ve 2.993 koşul** geçti. Tam test akışı `Editor/VersionTwentyValidation.cs` içindedir. Editor menüsü **Fener Runner → 00 - Verify workshop and friends v20 without APK**. Komut satırı girişi `FenerRunner.Editor.VersionTwentyValidation.ReviewAndExit`, asenkron akış tamamlandığında kendisi çıkar. Bu nedenle `-quit` ile birlikte kullanılmamalıdır.

Bu araçlar native Editor grafikleri gerektirir. Unity Test Framework testleri değildir. Tam akış test kimlikleriyle gerçek Friends ve Relay işlemleri içerir. Bulut kontrollerini bilinçli olarak yapılandırılmış bir geliştirme projesinde çalıştır. Ekransız veya çevrimdışı çalışmanın aynı kapsamı sağladığını varsayma. Ayrıntılar [çevrimiçi özellik geliştirme](MULTIPLAYER.tr.md#çevrimiçi-özellikleri-geliştirme) bölümündedir.

[Doğrulama belgesi](VALIDATION.md) temel sonuçları, test sınırlarını ve kalan cihaz kontrollerini kaydeder. Geçmişteki bir başarılı sonuç sonraki kod değişikliklerini veya yeni derlenen APK'yı otomatik olarak doğrulamaz.

## Repo düzeni

Kaynak kodu, sahne varlıklarını, `.meta` dosyalarını, paket kilitlerini ve amaçlanan belgeleri commit et. Unity önbelleklerini, yerel Editor durumunu, logları, derleme çıktılarını, imza anahtarlarını ve hizmet kimlik bilgilerini takip dışında tut. APK dosyaları kaynak ağacı yerine GitHub Releases bölümünde tutulur.

Dosyaları taşırken varlık GUID'lerini koru. Belirli bir konuya odaklanan commit'ler oluştur, davranış değişikliğini ve nasıl doğrulandığını açıkla. Haricî varlık eklemeden veya yeniden dağıtmadan önce [üçüncü taraf bildirimlerini](../THIRD_PARTY_NOTICES.md) incele.
