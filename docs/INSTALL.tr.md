# Kurulum ve oynanış

[English](INSTALL.md) · [Türkçe](INSTALL.tr.md) · [Projeye dön](../README.tr.md)

## İndir

[v0.20.0 sürüm sayfasından](https://github.com/ArdaHFO/Fener-Runner/releases/tag/v0.20.0) [FenerRunner-v0.20-arm64.apk](https://github.com/ArdaHFO/Fener-Runner/releases/download/v0.20.0/FenerRunner-v0.20-arm64.apk) dosyasını indir.

Önizleme sürümü **Android 8.0 veya üzeri ve 64 bit ARM cihaz** gerektirir. APK yaklaşık **34,8 MiB** boyutundadır ve ayrı bir OBB indirmesi gerekmez. iOS, Windows veya tarayıcı için kurulum paketi değildir.

## Telefona kur

1. APK'yı doğrudan telefonuna indir veya bilgisayarından telefona kopyala.
2. İndirilen dosyayı tarayıcının indirmeler ekranından ya da dosya yöneticisinden aç.
3. Android isterse dosyayı açan uygulamaya bu kaynaktan yükleme izni ver. Ekrandaki yönergeleri izleyerek kurulum ekranına dön.
4. **Yükle** seç. Uyumlu bir sürüm zaten kuruluysa **Güncelle** seçeneği görünür.
5. **Fener Runner**'ı aç ve tek kişilik koşuya başlamak için **SAHAYA ÇIK** seç.

Bu paket doğrudan yüklenen bir geliştirme önizlemesidir, Play Store sürümü değildir. Development Build kapalı olmasına rağmen APK yerel debug anahtarıyla imzalanmıştır. Android'in kurulum mesajları cihaza göre değişebilir.

## Önceki sürümden güncelle

Paket adı `com.fenerrunner.prototype`, sürüm `0.20.0` ve Android sürüm kodu `20`'dir. Kayıtlı v0.19 ve v0.12 derlemeleri aynı imza sertifikasını kullanır.

Uyumlu yerel ilerlemeyi korumak için mevcut uygulamanın üzerine kur. Uygulamayı kaldırmak veya verilerini temizlemek civcivleri, geliştirmeleri, seçili aracı ve anonim çevrimiçi kimliğe erişimi kaybettirebilir. Başka bir bilgisayarda derlenen APK genellikle farklı bir imza anahtarı kullanır ve bu indirmeye güncelleme olarak kurulamayabilir.

## Kontroller ve menüler

Oyunun mevcut arayüzü Türkçedir.

| Menü veya hareket | İşlev |
| --- | --- |
| **SAHAYA ÇIK** | Tek kişilik koşuyu başlat |
| **TRİBÜN ATÖLYESİ** | Marketi aç |
| **GÜÇLER / YETENEKLER / GARAJ** | Güçler, kalıcı yetenekler ve araçlar |
| **ARKADAŞINLA KOŞ** | Multiplayer ekranını aç |
| **ANA MENÜYE DÖN** | Mevcut koşuyu bitirip menüye dön |
| Sağa / sola kaydır | Şerit değiştir |
| Yukarı, ardından tekrar yukarı kaydır | Zıpla, ardından çift zıpla |
| Aşağı kaydır | Kay, art arda kayma da desteklenir |
| Hoparlör kontrolü | Ses düzeyini ayarla |

Koşuda topladığın civcivler atölyede kullanılır. Güçler toplandığında etkinleşir. Takip araçları kozmetiktir. Ara sıra çıkan tavuk civcivleri parkura saçar, ödülleri toplamak için hareket etmen gerekir.

Çevrimiçi oyun için [arkadaşlar ve multiplayer rehberini](MULTIPLAYER.tr.md) kullan. İki cihazda da internet ve aynı oyun sürümü gerekir.

## Dosyayı doğrula

Beklenen dosya boyutu **36.504.526 bayt**. Beklenen SHA-256:

```text
a8ced15d1ed41d80bb217e2947bffe469f84e63339c6f193ca882b3cd1b3c372
```

Windows'ta:

```powershell
Get-FileHash -Algorithm SHA256 .\FenerRunner-v0.20-arm64.apk
```

## Sorun giderme

| Sorun | Kontrol |
| --- | --- |
| Android APK'yı açamıyor veya kuramıyor | Android 8.0+, ARM64 desteği, yeterli depolama ve tamamlanmış indirme olup olmadığını kontrol et. Dosya özetini karşılaştır. |
| Güncelleme reddediliyor | Yerel olarak yeniden derlenmiş veya farklı imzalı bir sürüm kurulu olabilir. Nasıl değiştireceğine karar vermeden önce önemli uygulama verilerini koru. |
| Ses yok | Oyun içi ses düzeyini, Android medya sesini ve bağlı Bluetooth çıkışını kontrol et. |
| Arkadaş odaya katılamıyor | İki cihaz aynı sürümü kullanmalı. Bağlantıyı kontrol edip yeni davet veya oda kodu kullan. |
| Davet gelmiyor | İki uygulama da açık olmalı. Davetler iki dakika geçerlidir ve arka planda telefon bildirimi oluşturmaz. |

Fiziksel telefon performansı, Android klavyesi ve iki telefondan birlikte oynama kayıtlı sürüm doğrulamasına dahil değildir. Hata bildirirken telefon modelini, Android sürümünü, oyun sürümünü ve tam adımları ekle. [Doğrulama kapsamını](VALIDATION.md) ve [gizlilik belgesini](PRIVACY.md) inceleyebilirsin.
