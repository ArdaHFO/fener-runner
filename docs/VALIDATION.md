# Validation / Doğrulama

## Release baseline

The published APK is the existing **0.20.0** Android preview. Publication did not rebuild or change that binary. All 41 static APK checks were repeated successfully during publication. Its SHA-256 is:

```text
a8ced15d1ed41d80bb217e2947bffe469f84e63339c6f193ca882b3cd1b3c372
```

| Evidence | Result | Scope |
| --- | --- | --- |
| Native Unity Play Mode | 27 stages, 2,993 conditions passed | Pause/home, workshop, economy, vehicles, chick/hen collection, friends, Relay, movement, contacts, hitboxes, powers, audio, lifecycle, crowds and course generation. |
| Android artifact checks | 41 checks passed | Manifest, version, signing certificate, ZIP integrity, alignment and ARM64 native libraries. |
| Packaged service checks | 21 checks passed | Unity service configuration and required IL2CPP metadata/initializers. |
| Repository checks | Run separately by GitHub Actions | Source layout, assets/meta files, portable paths, excluded sensitive files and documentation links. No Unity execution in this workflow. |

The 2,993 conditions are project-specific regression assertions, **not 2,993 Unity Test Framework test cases**. The historical baseline combines successful stages 1–3, stage 4, and continuation 5–27 from three Editor sessions. Production code was unchanged across those runs. Focused test fixtures and orchestration were corrected between sessions. Two pre-existing temporary-job allocation warnings occurred during Editor shutdown. They are not presented as a warning-free device run.

A [sanitized evidence summary](validation/v0.20.0.json) retains stage counts and hashes of the original local reports. Raw local logs, machine paths and test-user records are deliberately not published. The summary's report hashes identify historical evidence, not publicly attached files.

For publication, four Editor-only capture helpers were changed to save under project-relative `Captures/` paths. Runtime gameplay code, scenes, art and package configuration match the validated local project. These path adjustments were statically checked. The complete native Unity suite was not rerun for repository publication.

## What this does not establish

- Physical Android installation, performance, battery use, heat, speakers or touchscreen behavior.
- A two-phone multiplayer soak test or service-availability guarantee.
- Play Store compliance or production signing. This non-development APK uses the project's local debug key.
- Exhaustive proof that no bugs remain.

Screenshots are native Unity Editor captures. The ghost-race picture uses a controlled loopback reference peer, not a second phone. See [media provenance](media/SOURCES.md).

See [development instructions](DEVELOPMENT.md) to run native checks. The complete suite requires configured live Unity services and creates temporary test identities/relationships. Repository CI intentionally performs only offline source checks.

## Türkçe

Yayımlanan APK, mevcut **0.20.0** deneme sürümünün aynı dosyasıdır. Bu yayın sırasında yeniden derlenmedi. Yayın hazırlığında 41 statik APK kontrolü yeniden çalıştırıldı ve tamamı geçti. Geçmiş doğrulamada **27 native Unity Play Mode aşamasında 2.993 koşul**, **41 APK kontrolü** ve **21 paketlenmiş hizmet kontrolü** geçti.

2.993 sayısı projeye özel doğrulama koşullarını ifade eder, Unity Test Framework test sayısını değil. Kanıt, aynı üretim koduyla çalıştırılan üç Editor oturumunun başarılı aşamalarından birleştirildi. Oturumlar arasında test düzenekleri ve çalıştırma akışı düzeltildi. Editor kapanışında önceden var olan iki geçici job bellek uyarısı görüldü.

Yayın kopyasında yalnızca dört Editor görüntü yakalama aracının kişisel dosya yolları taşınabilir hale getirildi. Oynanış kodu, sahneler ve varlıklar korunmuştur. Yayın hazırlığı için tam Unity testi yeniden çalıştırılmadı. GitHub Actions yalnızca kaynak düzeni ve dokümantasyon kontrollerini çalıştırır.

Fiziksel telefon performansı, dokunmatik davranışı ve iki telefonla uzun süreli yarış bu kanıtın kapsamında değildir. APK mağaza dağıtımı için üretim anahtarıyla imzalanmış değildir. Ekran görüntüleri gerçek Unity Editor sahnesindendir. Gölge yarış görselindeki ikinci oyuncu kontrollü bir yerel test eşidir.
