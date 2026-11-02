# Data and online features / Veriler ve çevrimiçi özellikler

This document describes the current implementation. It does not establish that a store privacy review has been completed.

## English

Solo progress, chick balance, purchased upgrades, selected vehicle and volume preferences are stored locally through Unity PlayerPrefs. There is no cloud save or account recovery interface for that progression in this version.

When you use friends or online races, the game connects to Unity Gaming Services Authentication, Friends and Relay. Anonymous authentication supplies a player identity. Friends uses your display name with its tag, relationships, online/busy presence and game invitations. Relay carries the two-player race connection, including ghost movement and race state. Services necessarily receive network connection data to operate.

The friends connection is remembered after its first successful use and can reconnect when you reopen the game. Invitations are received while the application is open. There are no operating-system push notifications. Invitations expire after two minutes. The app does not read your phone address book or email contacts to find friends.

Uninstalling the application or clearing its storage can remove both local progress and the credentials needed to access an anonymous multiplayer identity. There is no implemented in-game account deletion or recovery flow. Removing a friendship is not deletion of the underlying service account. For service-provider information, see [Unity's privacy policy](https://unity.com/legal/privacy-policy).

The source repository does not include the maintainer's signing key, saved player session tokens, local PlayerPrefs, or live test-user records. Its Unity project ID is a public service routing identifier, not an authentication secret. Developers running their own distribution should configure their own Unity Cloud project.

## Türkçe

Tek oyunculu ilerleme, civciv bakiyesi, satın alınan geliştirmeler, seçilen araç ve ses tercihleri Unity PlayerPrefs ile cihazda saklanır. Bu sürümde ilerleme için bulut kaydı veya hesap kurtarma arayüzü bulunmaz.

Arkadaşlar ve çevrimiçi yarışlar Unity Gaming Services Authentication, Friends ve Relay hizmetlerini kullanır. Anonim oturum bir oyuncu kimliği oluşturur. Friends, etiketli görünen adını, arkadaşlık ilişkilerini, çevrimiçi/meşgul durumunu ve oyun davetlerini kullanır. Relay gölge hareketi ve yarış durumu dahil iki oyunculu bağlantıyı taşır. Hizmetlerin çalışması için ağ bağlantı bilgileri de hizmet sağlayıcısına ulaşır.

İlk başarılı kullanımın ardından arkadaş bağlantısı hatırlanır ve oyun yeniden açıldığında bağlanabilir. Davetler yalnızca oyun açıkken alınır ve iki dakika sonra sona erer. Telefon bildirimi gönderilmez. Arkadaş bulmak için telefon rehberi veya e-posta kişileri okunmaz.

Uygulamayı kaldırmak ya da verilerini temizlemek, ilerlemeyi ve anonim oyuncu kimliğine erişimi kaybettirebilir. Oyun içi hesap silme veya kurtarma akışı henüz yoktur. Bir arkadaşlığı kaldırmak hizmet hesabını silmez. Sağlayıcı bilgileri için [Unity gizlilik politikasına](https://unity.com/legal/privacy-policy) bakabilirsin.

Repoda geliştiricinin imza anahtarı, kayıtlı oturum belirteçleri, yerel oyuncu kayıtları veya canlı test kullanıcısı verileri bulunmaz. Unity proje kimliği bir yönlendirme bilgisidir, gizli kimlik doğrulama anahtarı değildir. Ayrı dağıtım yapan geliştiriciler kendi Unity Cloud projelerini yapılandırmalıdır.
