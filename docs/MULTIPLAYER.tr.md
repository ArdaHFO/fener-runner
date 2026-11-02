# Arkadaşlar ve gölge yarışları

[English](MULTIPLAYER.md) · [Türkçe](MULTIPLAYER.tr.md) · [Projeye dön](../README.tr.md)

## Nasıl çalışır?

İki oyuncu aynı başlangıç değerinden üretilen parkurda koşarken birbirini yarı saydam gölge olarak görür. Her cihaz kendi hareketini, çarpışmalarını, toplanan nesnelerini ve cüzdanını yönetir. Uzaktan gelen durum paketleri gölgeyi hareket ettirir, yerel oyuncuya para kazandırmaz veya onu kontrol etmez.

Oyun anonim kimlik doğrulama, Friends ve Relay için Unity Gaming Services kullanır. Oyuncuların kendi sunucularını kurması veya yönlendiricide port açması gerekmez. Çevrimiçi oyun için internet ve hizmetlerin erişilebilir olması gerekir.

Bu sistem iki kişilik gölge yarışıdır, ortak çarpışma simülasyonu değildir. İki oyuncunun oyun sürümleri uyumlu olmalıdır. Arka planda telefon bildirimi hizmeti yoktur.

## Arkadaş ekle ve davet et

İki oyunu da açık ve internete bağlı tut.

1. Ana menüden **ARKADAŞINLA KOŞ → ARKADAŞLARIM · DAVETLER** ekranını aç. Gerekirse **BAĞLAN** seç.
2. Ekranın üstündeki tam oyuncu adını kopyala. Ad, `ad#etiket` biçimindedir.
3. Arkadaşının tam adını gir ve **EKLE** seç.
4. Karşı oyuncu **İSTEKLER** ekranında **KABUL ET** seçsin.
5. Arkadaşın çevrimiçiyken adının yanından **DAVET ET** seç. Gerektiğinde boş bir oda oluşturulur.
6. Alıcı **DAVETLER** ekranından **KATIL** seçsin.
7. İki oyuncu da odada **HAZIRIM** seçsin. Ortak geri sayımdan sonra yarış başlar.

Davet **iki dakika** geçerlidir. Süresi dolarsa yenisini gönder. Davetler arasında kısa bir bekleme süresi bulunur. Odaya katılmak her zaman alıcının seçimine bağlıdır, gelen davet otomatik katılım başlatmaz.

## Oda koduyla katıl

**ARKADAŞINLA KOŞ** ekranındaki oda oluşturma ve oda koduyla katılma kontrollerini kullan. Odayı kuran oyuncu ekrandaki kodu ikinci oyuncuyla paylaşır. Bağlantı kurulduktan sonra ikisi de **HAZIRIM** seçer.

Başka bir odaya gelen daveti kabul etmeden önce mevcut odadan ayrıl. Multiplayer koşusundan ana menüye dönmek de o odadan ayrılmanı sağlar.

## Kimlik ve yerel ilerleme

Kimlik doğrulama anonimdir ve cihazda korunur. Kabul edilmiş arkadaşlıklar çevrimiçi Friends hizmetinde saklanır. Yerel ilerleme cihazda ayrı olarak tutulur.

Uygulamayı kaldırmak veya verilerini temizlemek anonim kimliğe ve arkadaş listesine erişimi kaybettirebilir. Oyuncu adı bir hesap kurtarma bilgisi değildir. Bu önizlemede kullanıcıya sunulan hesap bağlama veya kurtarma akışı bulunmaz. Ayrıntılar [gizlilik belgesinde](PRIVACY.md).

## Çevrimiçi özellikleri geliştirme

Kendi dağıtımın için geliştirme kopyasını yönettiğin bir Unity Cloud projesine bağla ve **Authentication, Relay ve Friends** hizmetlerini yapılandır. Test etmeden önce sabitlenmiş paketleri yükle. Hizmet hesabı kimlik bilgilerini istemciye veya repoya eklemek yerine Unity Editor'ün proje/hizmet ayarlarını kullan.

Yayımlanan APK, sürümün yapılandırılmış arka ucunu korur. Başka bir bulut projesine bağlanan fork farklı bir çevrimiçi ortama aittir. Kimliklerinin, arkadaşlarının veya oda kodlarının indirilebilir sürümle birlikte çalışacağını varsayma.

Gerçek bulut doğrulaması test kimlikleri oluşturur ve hizmet üzerinde gerçek işlemler yapar. Bunu bilinçli olarak geliştirme projen üzerinde çalıştır. Geçmiş kurulum kontrolleri sürüme özel proje kimliklerini veya sürüm değerlerini sınayabilir. Araçları başka bir arka uca uyarlarken bu varsayımları incele.

İletişim ve durum akışını incelemek için şu klasörden başla:

```text
UnityProject/Assets/FenerRunner/Runtime/Multiplayer/
```

`RunnerMultiplayer`, kimliği doğrulanmış Relay oturumlarını ve koşu durumunu yönetir. `RunnerFriends`, arkadaşlıkları ve davet mesajlarını işler. `GhostRunnerView`, uzaktaki koşucuyu gösterir. Protokol uyumluluk kontrolleri, sınırlandırılmış mesajlar ve uzaktaki durum paketlerinin işlenmesini içerir.

## Neler doğrulandı?

v0.20 kanıtları, Editor test kimlikleriyle gerçek Friends isteği/kabulü, kalıcılık, çevrimiçi durum ve davetleri, ayrıca gerçek Relay bağlantısını kapsar. Native sahne oynanışı kontrollü bir loopback eş düğümüyle de test edildi.

Galerideki gölge görüntüsü bu yerel eş düğümü senaryosundan gerçek bir Editor görüntüsüdür. İki fiziksel telefonda çalışmayı, mobil ağ güvenilirliğini veya Android performansını kanıtlamaz. Tam [doğrulama kapsamını](VALIDATION.md) inceleyebilirsin.
