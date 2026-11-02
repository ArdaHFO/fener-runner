# Kartal — koşucu karakter, v0.1

Bu paket, gönderilen tek referans görsel yorumlanarak hazırlanan ilk stilize 3D karakter sürümüdür. Sarı büyük kafa, belirgin kaşlar, turuncu gaga, kısa kuş bacakları ve krem örgü kazak korunmuştur. Referansta görünmeyen arka yüz ve kuyruk tasarım yorumudur. Görsel benzerlik ve animasyon karakteri birlikte değerlendirildikten sonra ayrıntılar değiştirilebilir.

## Dosyalar

| Dosya | Kullanım |
|---|---|
| Kartal_Runner_v01.glb | Geometri, iskelet, malzemeler, iki doku ve yedi animasyon içeren taşınabilir model. Harici dosya bağlantısı yoktur. |
| Kartal_Runner_v01.fbx | Unity gibi oyun motorlarına aktarmak için model ve animasyonlar. Dokular ayrıca textures klasöründe sağlanır. |
| Kartal_Runner_v01.blend | Düzenlenebilir Blender kaynağı. Model, iskelet, Actions ve önizleme sahnesi bulunur. |
| Kartal_Animasyon_Onizleme.mp4 | Gerçek modelin koşu, dönüş, takla, zıplama, kayma ve poz hareketleri. |
| Kartal_hero.png / front / side / back / run_pose | Gerçek modelden alınmış görünüşler. |
| textures/ | Örgü temel renk ve normal dokuları, 1024 × 1024 PNG. |
| source/ | Modeli, iskeleti, animasyonları ve çıktıları tekrar üretmek için Python betikleri. |
| asset_manifest.json, asset_validation.json, qa/ | Sayımlar ve dışa aktarılan dosyalar üzerinde yapılan kontroller. |
| CODEX_ENTEGRASYON_NOTU.txt | İleride oyuna bağlama işi için kapsam ve davranış notu. |

## Animasyonlar

| Klip | Süre | Önerilen kullanım |
|---|---:|---|
| Idle | 2,0 sn | Nefes ve küçük kafa hareketi; döngü. |
| Run | 0,8 sn | Kısa adımlı koşu; döngü. |
| Jump | 0,9 sn | Kalkış, havada poz ve toparlanma; tek sefer. |
| Slide | 0,8 sn | Gövdeyi alçaltarak kayma; tek sefer. |
| RunTurn | 1,0 sn | Koşu sırasında 360° dönüş; tek sefer. |
| JumpFlip | 1,0 sn | Öne doğru tam takla; tek sefer. |
| CelebratePoint | 1,5 sn | Referanstaki tavrı çağrıştıran kol uzatma pozu; tek sefer. |

Klipler yerinde oynar: root kemiği dünyada ilerlemez. İleri hareket, şerit değiştirme, gerçek zıplama yüksekliği ve çarpışma oyun kontrolcüsüne aittir. Takla ve dönüş pelvis/iskelet üzerinde oynar; oyun kökünün yönünü değiştirmez.

Önizlemede klipler arasında yaklaşık 0,167 saniyelik poz karışımı vardır. Zıplama ve taklada kontrolcü örneği olarak modelin bütününe dikey yay eklenmiştir. Bunlar videonun gösterim düzenidir; dosyaya hazır bir oyun kontrolcüsü eklenmemiştir. Oyunda geçişler ayrıca bağlanmalıdır. Video 24 fps'dir; bir telefon performans ölçümü değildir.

## Unity için başlangıç ayarları

1. FBX ve textures klasörünü projenin Assets klasöründe karaktere ayrılmış bir klasöre ekleyin.
2. FBX içe aktarma panelinde Rig → Animation Type → Generic seçin.
3. Avatar Definition → Create From This Model seçin; Root node olarak root kemiğini kullanın.
4. Animation sekmesinde Import Animation seçeneğini açın ve yedi klibi kontrol edin.
5. FBX içe aktarıcısı kliplere Kartal_Rig gibi önekler ekleyebilir. Son bölüm Run, JumpFlip vb. olan klipleri yukarıdaki kısa adlarla eşleyin.
6. Tasarım önerisi: Idle ve Run için Loop Time açık, diğer klipler için kapalı; Animator Apply Root Motion kapalı olsun. Bu paket için kontrolcü dünyadaki hareketin sahibidir.
7. Kullanılan render pipeline'a göre malzemeleri eşleyin. Örgü basecolor dokusunu temel renge, kartal_knit_normal.png dosyasını normal haritasına bağlayın; normal haritasının içe aktarma türünü kontrol edin.

Generic içe aktarma adımları Unity'nin resmi belgesine dayanır. Döngü, root motion ve geçiş tercihleri bu koşucu tasarımı için öneridir. Bu teslimat Unity içinde veya fiziksel telefonda çalıştırılmamıştır.

Resmi başvuru: https://docs.unity3d.com/6000.1/Documentation/Manual/GenericAnimations.html

## Hareketin oyunda akıcı kalması

Başlangıç için 0,10–0,18 saniyelik animasyon geçişleri denenebilir; bunlar ayarlanacak tasarım değerleridir. Otomatik gösteri hareketlerini tehlike olmayan boş aralıklarda tetikleyin. Oyuncu zıplama, kayma veya şerit değiştirme komutu verdiğinde gösteri hareketi kontrolü geciktirmemelidir. Takla görselinin dönmesi, çarpışma kapsülünü de döndürmemelidir. Kayma kapsülünün boyu ise ayrı bir oyun mekaniğidir. Aynı süredeki zıplama varyantlarının fiziksel yükselişi aynı kontrolcü eğrisine bağlanmalıdır.

## Gerçek sayımlar ve doğrulama

- Blender kaynak geometrisi: 15,046 köşe, 29,328 üçgen.
- GLB geometrisi: 16,683 köşe, 29,328 üçgen. Dışa aktarımda UV/normal sınırlarında köşeler ayrılabildiğinden köşe sayıları farklıdır.
- 1 mesh nesnesi, 19 iskelet kemiği, 15 malzeme/mesh primitive.
- İki gömülü 1024 × 1024 doku; GLB 1.74 MiB.
- GLB yapısı, skin ağırlıkları, kemik referansları, animasyon süreleri ve harici dosyaya bağımlı olmaması denetlendi.
- RunTurn ve JumpFlip dışa aktarılmış quaternion örneklerinde tam 360° dönüyor.
- FBX temiz bir Blender oturumuna tekrar alınarak kemikler, skin bağlantıları ve ayrı animasyonlar kontrol edildi.
- Blender kaynak ve önizleme üretimi: 4.5.15 LTS.

Bu ilk sürümde ayrıca malzeme atlası, LOD seviyeleri, yüz mimik sistemi, parmak rig'i veya kumaş simülasyonu yoktur. Mobil hedefte özellikle 15 malzeme ve sahneyle birlikte toplam çizim maliyeti profillenmelidir. Görsel benzerlik, animasyonların oyun hızına uyumu ve cihaz performansı sonraki değerlendirme konularıdır.

## Kaynağı yeniden üretmek

Blender 4.5 LTS ve komut satırında Python 3 kullanın. Blender'ın kendi Python ortamındaki bpy ve numpy yeterlidir. Paket açıldıktan sonra karakter klasöründe:

    blender --background --factory-startup --threads 8 --python source/build_character.py

Betikler output/ klasöründe yeni çıktılar üretir. Mevcut modelden video karelerini üretmek için:

    blender --background Kartal_Runner_v01.blend --threads 8 --python source/render_saved.py -- --motion

FFmpeg kuruluysa bu karelerden MP4 üretmek için:

    python source/encode_preview.py

GLB yapısal kontrolü:

    python source/validate_glb.py Kartal_Runner_v01.glb --require-in-place --expect-clip Idle --expect-clip Run --expect-clip Jump --expect-clip Slide --expect-clip RunTurn --expect-clip JumpFlip --expect-clip CelebratePoint --report yeniden_kontrol.json
    python source/audit_rotations.py Kartal_Runner_v01.glb --merge-report yeniden_kontrol.json

Model/rig/animasyon kaynak betikleri teslimata dahildir. Sahne ışıkları, kamera ve zemin GLB/FBX oyun varlığının parçası değildir.
