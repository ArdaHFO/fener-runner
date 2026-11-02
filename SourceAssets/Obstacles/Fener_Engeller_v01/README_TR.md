# Fener — iki özel kuş engeli, v0.1

İki kullanıcı referansından yorumlanmış, sabit pozlu 3D oyun engelleri. Oyuncu karakteri ve oyun projesi bu pakete dahil değildir.

## İki rol

**Siyah kuş / üzerinden atlama:** Yuvarlak koyu baş ve gövde, büyük gözler, koyu gaga, sarı-lacivert dikey çizgili forma ve küçük göğüs rozeti korunarak alçak çömelme pozu oluşturuldu. Kanatlar bükülmüş dizlere doğru uzanır. Yüksekliği yaklaşık 1,04 m.

**Kıvırcık saçlı sarı kuş / altından kayma:** Koyu dalgalı-kıvırcık saç, sarı yüz, göz/kaş ve gaga özellikleri, kısa kollu yakalı çizgili forma korundu. Geniş bacak açıklığı ve yükseltilmiş gövde, mevcut oyuncunun Slide pozuna alan açar. Bu oyun işlevi için karakterin alt gövde oranları ve ölçeği büyütülmüştür; toplam yüksekliği yaklaşık 3,66 m.

Tek ve küçük referans görsellerden yapılan stilize yorumlardır. Küçük forma rozetleri ve görünmeyen arka ayrıntılar tasarım yorumları içerir. İlk görünüş değerlendirmesi sonrasında yüz/saç/oranlar değiştirilebilir.

## Dosyalar

| Dosya kökü | Rol |
|---|---|
| Engel_Atlama_SiyahKus_v01 | JumpOver: üzerinden atlama |
| Engel_Kayma_KivircikKus_v01 | SlideUnder: açık geçidin altından kayma |

Her karakterin FBX, GLB ve düzenlenebilir Blender (.blend) dosyası vardır. GLB dokularını içerir; dokular ayrıca textures/ klasöründedir. FBX dosyalarına dokular gömülmüştür ve ayrıca sağlanmıştır.

Bu iki varlık **statik, poz verilmiş mesh** olarak hazırlanmıştır. Rig veya animasyon klibi içermez. Ana kartalın Generic iskelet ayarlarını bu engellere kopyalamayın. Unity'de FBX'leri statik model olarak içe alın; Import Animation kapalı tutulabilir. Malzemeleri projenin render pipeline'ına göre eşleyin.

Hero/front/side PNG'ler gerçek modellerden alınmıştır. Kayma_Gecis_Onizleme.png ve Atlama_Gecis_Onizleme.png mevcut kartalın kaynak animasyon pozlarını aynı sahnede gösterir. Bu görseller bir fizik motoru veya telefon testi değildir.

## Ölçek ve gerçek ölçüler

Kaynak Blender koordinatları: X sağ, Z yukarı, karakterlerin önü -Y. Dışa aktarılan GLB ve collider_layout.json: Y yukarı, X sağ, Z ileri. Birim metredir. Model kökü zemindedir. Oyuncunun ilerleme yönüne göre engelin yüzünü oyuncuya çevirmek için bütün prefabın yönünü kontrol edin; görsel ve collider aynı ebeveynde dönmelidir.

| Özellik | Siyah kuş | Kıvırcık kuş |
|---|---:|---:|
| Genişlik | 1,228 m | 2,820 m |
| Yükseklik | 1,044 m | 3,663 m |
| Derinlik | 0,890 m | 1,290 m |
| Üçgen | 15,922 | 36,702 |
| Malzeme | 10 | 9 |
| Runtime mesh nesnesi | 1 | 1 |

Kıvırcık karakterin geçişi **1,55 m genişlik × 1,66 m yükseklik** boyunca, modelin bütün derinliğinde açıktır. Dışa aktarılan her üçgen bu geçişe karşı kontrol edildi; içeri giren üçgen sayısı 0.

Önceden hazırlanan kartalın en alçak Slide pozunun genişliği1,216 m, yerden en yüksek noktası1,446 m. Ortalanmış geçişte her yanda yaklaşık 16,7 cm, üstte 21,4 cm geometrik pay kalır. Aynı kartalın bekleme yüksekliği yaklaşık 2,028 m olduğu için ayakta geçiş açıklığı aşar.

Bu ölçüler oyuncunun ve engellerin scale=1 durumuna aittir. Kayma engelini tek başına küçültmek açıklığı da küçültür. Şerit genişliği ve karakter ölçeği birlikte değerlendirilmelidir; kıvırcık kuşun yaklaşık 2,82 m'lik ayak açıklığını dar bir şeride rastgele sıkıştırmayın.

## Çarpışma alanları

collider_layout.json her model için yerel merkez/boyut değerleri verir:

- Siyah kuş: Body adlı tek muhafazakâr kutu.
- Kıvırcık kuş: LeftLeg, RightLeg ve TorsoHeader adlı üç ayrı kutu.

Kayma modeline tek bir çevreleyen BoxCollider veya modelin tümünü kaplayan convex hull uygulanırsa açık geçit kapanır. Üç parçalı düzen ortadaki boşluğu korur. Bu JSON önerilen geometrik yerleşimdir; Unity prefabı veya çalışır çarpışma kodu değildir. Trigger/solid davranışı oyunun kontrolcüsüne göre bağlanmalıdır.

## Zıplama ve kayma zamanlaması

**Kayma:** Kaynak Slide klibi 0,8 sn sürer. Ölçülen pozlar geçide yaklaşık0,12–0,64 sn aralığında sığar. Bu aralık animasyonlar arası karışımı içermez. Kartalın tüm gövdesi engeli geçmeden ayağa kalkmaması gerekir. Başlangıç collider önerisi olarak kayma kapsülü yüksekliği en fazla 1,55 m ve çapı en fazla 1,30 m düşünülebilir; gerçek kontrolcüde test edilmelidir.

**Atlama:** Siyah engelin tepesi1,043624 m'dir. Taklada kafa animasyon kökünün altına döndüğü için yalnızca ayakların yükselmesini kontrol etmek yeterli değildir. Kaynak karakter ölçeğinde, engelle tüm boyuna örtüşmenin uçuşun normalize 0,35–0,65 aralığında kaldığı bir sinüs yayı için:

- Jump:10 cm görsel pay için yaklaşık 1,1315 m tepe yüksekliği gerekir.
- JumpFlip:aynı pay için yaklaşık 1,9813 m gerekir.
- Ortak ayarlanabilir 2,10 m tepe yüksekliği bu aralıkta taklanın en alçak geometrisine yaklaşık 21,8 cm pay verir.

Bu sonuçlar **geometriye ve belirtilen zaman aralığına dayalı başlangıç değerleridir**. Oyun hızı, engel derinliği, giriş zamanı, gerçek zıplama eğrisi, animasyon geçişleri ve CharacterController kapsülü Unity'de birlikte doğrulanmalıdır. Rastgele her anda başlatılan bir zıplamanın geçeceği iddia edilmez.

## Yapılan doğrulama

- İki GLB'nin geometri/buffer/index/malzeme referansları ve gömülü dokuları kontrol edildi.
- Normal vektörleri ve sonlu koordinatlar kontrol edildi.
- Kayma koridoru dışa aktarılan üçgenlere karşı test edildi; sıfır giriş.
- Aynı koridor mevcut oyuncunun Slide geometrisiyle karşılaştırıldı.
- FBX dosyaları temiz Blender oturumunda tekrar içe aktarıldı; üçgenler, normaller ve boyutlar GLB ile uyumlu.
- Collider kutuları son modellerin boyutları ve açık koridorla karşılaştırıldı.

Unity sahnesi, oyun içi çarpışma, dokunmatik kontrol ve gerçek cihaz performansı bu teslimatta çalıştırılmamıştır. Statik engellerin sayısı, 9/10malzemelik çizim maliyeti ve görünüş kalitesi hedef sahnede profillenmelidir.

Raporlar: BlackBird_validation.json, CurlyBird_validation.json, Collision_validation.json ve qa/ klasörü.

## Kaynağı yeniden üretmek

Blender 4.5 LTS ile paket klasöründe:

    blender --background --factory-startup --threads 8 --python source/build_obstacles.py

Bu komut output/ altında yeni GLB/FBX/Blend ve renderlar üretir. İki modelin kaynakları ayrı ve düzenlenebilirdir. Kamera/ışık/zemin oyun varlığı olarak dışa aktarılmaz.

GLB kontrolü (Python 3 standart kütüphane):

    python source/validate_obstacles.py Engel_Atlama_SiyahKus_v01.glb --report siyah_kontrol.json
    python source/validate_obstacles.py Engel_Kayma_KivircikKus_v01.glb --gate-width 1.55 --gate-height 1.66 --player-report qa/player_clearance.json --report kayma_kontrol.json

Kayma/atlama birlikte görünüşlerini tekrar üretmek için önce modelleri output/ içine üretin; sonra önceki kartal paketindeki Blender kaynağını verin:

    blender --background --factory-startup --threads 8 --python source/render_clearance.py -- --player-blend /tam/yol/Kartal_Runner_v01.blend
