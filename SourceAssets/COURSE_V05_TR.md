# v0.5 parkur ve zorunlu hareket denetimi

v0.5 parkuru, şerit değiştirmenin tek başına yeterli kaldığı uzun açılışı kaldırır. İlk sırada z=70 m'de üç şeritte siyah kuş, ikinci sırada z=115 m'de üç şeritte kayma kapısı vardır. Merkez şeritteki ilk iki modelin sırası korunur. Güçlendirme olmadan ilk sırada zıplama, ikinci sırada kayma gerekir.

Daha sonraki beş sıralık bölümler, dar bir şerit değişimi, bütün şeritlerde zıplama, farklı hareketler arasında seçim, bütün şeritlerde kayma ve kısa bir nefes aralığı içerir. Renkli ödül hattı izlenebilir rotayı gösterir. Yedi kalıp ailesi şerit ve karışık sıra seçimini değiştirir. Tek tip zorunlu zıplama ve kayma sıraları rastgele seçime bırakılmaz. Her iki hareket, en fazla 240 m içinde tekrar gerekir. Uçuş ve çarpışma koruması gibi güçlendirmeler kendi süreleri boyunca bu engelleri bilinçli olarak aşabilir.

Düzen en yüksek 30 m/sn hız için ayarlanmıştır. Boş rota sıraları arasında en az 30 m, hareket gereken sıralar arasında en az 45 m bulunur. Bu, en yüksek hızda sırasıyla 1 sn ve 1,5 sn'dir. Zıplama süresi 0,9 sn, kayma süresi 0,85 sn, tek şerit geçişi 0,14 sn olarak değerlendirilir. Nefes aralığı ek 6 m bırakır. Şerit değişimi bir sırada en çok bir şerittir. Engeller arasındaki jeton hattı önceki sıradan 7 m sonra yön değiştirmeye başlar, sonraki sıradan 14 m önce hedef şeride ulaşır.

## Tekrar çalıştırma

PowerShell 7'de yeni bir oturumda `Tools/Audit-CoursePatterns.ps1` dosyasını çalıştırın. Bu araç doğrudan teslim edilen C# üreticisini derler ve yürütür. Unity içinde Fener Runner menüsündeki **11 - Audit procedural route seeds** aynı denetimi çalıştırır.

- C# algoritma raporu (`pattern_algorithm_audit_v5.json`, tarihsel yerel rapor)
- Unity içinden algoritma raporu (`unity_pattern_audit_v5.json`, tarihsel yerel rapor)

Denetim 4.096 tohum × 512 sıra, toplam 2.097.152 sırayı inceler. Aynı tohumla aynı dizinin oluşması, izlenebilir rotanın erişilebilirliği ve 30 m/sn hızdaki zaman bütçeleri kontrol edilir. Ayrıca ayrı durum kümeleriyle yalnız şerit değiştiren, yalnız zıplayabilen ve yalnız kayabilen oyuncuların kalan rotaları hesaplanır. Güçlendirmeler bu temel denetimde kapalı varsayılır.

Gerçek C# çalıştırmasında her tohumda yalnız şerit değiştiren rota 70 m'de, zıplayıp kaymayan rota 115 m'de, kayıp zıplamayan rota 70 m'de tükendi. Ölçülen en büyük zorunlu zıplama aralığı 228,49 m, kayma aralığı 228,51 m oldu. Herhangi bir zorunlu tam sıra arasındaki en büyük aralık 134,51 m idi. Hata listesi boştu. Bu sonuç üç şeritli ayrık rota modeline aittir. Gerçek kapsül, model geometrisi, kaydırma zamanlaması ve güçlendirmelerin etkileşimi ayrıca Unity Play Mode'da denetlenir. Fiziksel telefon performansı iddiası içermez.

Bu üretici, [SYBO'nun resmî kaydırma kontrolleri](https://sybo.helpshift.com/hc/en/5-subway-surfers/faq/205-basics/) ve [Poki'deki SYBO oyun açıklamasındaki](https://poki.com/en/g/subway-surfers) ardışık engel/ödül yaklaşımından esinlenen proje kodudur. Subway Surfers'ın özel üretim algoritmasına erişildiği veya aynı kodun kullanıldığı iddia edilmez.


> Bu belge önceki geliştirme aşamasının kaynak notudur. Ham yerel test raporları açık repoya eklenmez. Güncel doğrulama kapsamı için [yayın doğrulama belgesine](../docs/VALIDATION.md) bakın.
