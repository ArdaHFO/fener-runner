# Oynanış referansları ve v0.4 parkur kararı

31 Ekim 2026 tarihinde incelenen kaynaklar.

## Resmî hareket referansı

[SYBO — Subway Surfers Help Center, Basics](https://sybo.helpshift.com/hc/en/5-subway-surfers/faq/205-basics/) sola/sağa kaydırmayı yön değişimi, yukarı kaydırmayı zıplama, aşağı kaydırmayı yerde yuvarlanma ve havada zıplamayı kesme olarak açıklar. Fener Runner v0.4 bu dört yönlü kontrol düzenini kullanır. Havada aşağı kaydırma hızlı iniş ve devamında kayma üretir. Hareket düğmeleri oyun ekranından kaldırılmıştır.

Aynı kaynakta bulunan çift dokunmayla hoverboard açma özelliği bu sürümde uygulanmadı.

## Engel, tempo ve ödül referansı

[Subway Surfers — Poki, SYBO tarafından yayımlanan oyun](https://poki.com/en/g/subway-surfers), art arda kaçınılan engelleri, ilerledikçe yükselen tempoyu, jeton hatlarını ve kısa süreli güçlendirmeleri açıklar. Bu açıklamalardan alınan tasarım ilkeleri, okunabilir bir girişten sonra daha yoğun kararlar sunmak, jetonlarla bir rota göstermek ve kısa ödül anları yaratmaktır.

Fener Runner'da bu ilkeler kendi karakterleri ve kendi üreticisiyle uygulanır. Dört ısınma sırasını yedi farklı kalıp ailesi izler. Üç baskı sırasından sonra bir nefes aralığı gelir. Birden fazla şerit aynı anda engellenebilir. Tam genişlikteki sıralar zamanında zıplama veya kayma ister. Her sıra, fizik ayarlarıyla uyumlu bir rota ve eylem bilgisi taşır. Üretici, en yüksek 27 m/sn hız için en az 28 m boş rota aralığı ve en az 43 m eylem aralığı ayırır. Jeton yayı zıplamayı, şerit hattı yön değişimini gösterir.

Bu, Subway Surfers'ın özel kaynak kodu veya gizli engel üretme algoritmasının kopyası değildir. Buradaki tohumlu kalıp sistemi, aralıklar, kurtarılabilir rota kuralları ve Tribün Ateşi ödülü bu proje için geliştirilmiştir. Kaynaklarda yayımlanmayan bir algoritma hakkında doğruluk iddiası yoktur.

## Denetim ve sınırlar

`CoursePatternAudit`, aynı tohumu yeniden üretmeyi, şerit erişilebilirliğini, eylem eşleşmesini ve zaman aralıklarını 2.048 tohum × 256 sıra üzerinde inceler. Unity parkur raporu (`unity_pattern_audit.json`, tarihsel yerel rapor), ek çeşitlilik/jeton zarfı raporu (`pattern_diversity_and_reward_envelope.json`, tarihsel yerel rapor) ve gerçek Play Mode engel raporu (`unity_obstacles_v4.json`, tarihsel yerel rapor) farklı doğrulama kapsamlarını açıkça belirtir.

Tohum denetimi gerçek fizik, cihaz dokunmatiği veya telefon performansı ölçümü değildir. Geçişler ayrıca gerçek Unity kapsülü ve karakter geometrisiyle kontrol edilir. Fiziksel telefonda oynanış ve FPS testi henüz yapılmadı. Bu sürümde APK oluşturulmadı.


> Bu belge önceki geliştirme aşamasının kaynak notudur. Ham yerel test raporları açık repoya eklenmez. Güncel doğrulama kapsamı için [yayın doğrulama belgesine](../docs/VALIDATION.md) bakın.
