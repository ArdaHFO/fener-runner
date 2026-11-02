# Contributing / Katkıda bulunma

## English

Start with [development setup](docs/DEVELOPMENT.md) and the [validation scope](docs/VALIDATION.md). Keep gameplay changes focused and preserve Unity `.meta` files when moving or renaming assets.

For a bug report, include the game version, device/Android version, exact steps, expected and actual behavior, and a screenshot or short recording when useful. For movement bugs, include the swipe direction, approximate speed, active powers and obstacle type. Remove player IDs, session tokens and private paths from logs before attaching them.

Before opening a pull request, run `python scripts/verify-repository.py`. For gameplay changes, run the relevant native Unity checks and describe the device testing actually performed. Clearly distinguish source checks, Editor checks and phone tests. New art/audio needs a stated source and license in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Never commit signing keys, Unity caches, APKs, account tokens or personal runtime state. Installable artifacts belong on Releases. The current APK is a preview and its signing key is intentionally private.

## Türkçe

[Geliştirme kurulumunu](docs/DEVELOPMENT.tr.md) ve [doğrulama kapsamını](docs/VALIDATION.md) incele. Değişiklikleri tek bir amaca odakla. Unity varlıklarını taşırken `.meta` dosyalarını koru.

Hata bildirimine sürüm, telefon/Android modeli, tekrar adımları, beklenen ve gerçekleşen davranışı ekle. Hareket hatalarında kaydırma yönü, yaklaşık hız, etkin güçler ve engel türü önemlidir. Paylaştığın günlüklerden oyuncu kimliklerini, oturum belirteçlerini ve kişisel yolları çıkar.

Pull request öncesi `python scripts/verify-repository.py` çalıştır. Oynanış değişiklikleri için ilgili Unity kontrollerini ve gerçekten yaptığın telefon testlerini belirt. Yeni görsel/seslerin kaynak ve lisans bilgilerini ekle. İmza anahtarlarını, önbellekleri, APK'ları veya hesap verilerini Git'e ekleme.
