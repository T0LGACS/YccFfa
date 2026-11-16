# YccFfa

FFA (herkes herkese) deathmatch sunucusu için silah turları: belli aralıklarla (varsayılan 3 dakika) herkesin silahı değişir (AK-47, M4A1-S, AWP, Deagle, Scout, Galil). Değişimden önce sohbette sıradaki tur duyurulur, değişince hayattakilerin (botlar dahil) silahı anında yenilenir. Öldürünce can ve şarjör yenilenir, öldürme serileri duyurulur, harita süresi bitince sıradaki haritaya geçilir.

> FFA deathmatch weapon rounds: everyone's weapon changes every few minutes, kill rewards, kill-streak announcements and map rotation.

[YCC TEAM](https://yccteam.com.tr) CS2 sunucusu için sıfırdan yazıldı ve sunucuda her gün çalışıyor. Başka eklentilerden kod alınmadı.

## Gereken

- CS2 sunucusu, [Metamod:Source](https://www.sourcemm.net/) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) (1.0.371 ve üstü)

## Kurulum

1. Sunucuda [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) ve [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) (with-runtime sürümü) kurulu olmalı.
2. [.NET 10 SDK](https://dotnet.microsoft.com/download) ile derle: `dotnet build -c Release`
3. `bin/Release/net10.0/` içindeki her şeyi sunucuda `addons/counterstrikesharp/plugins/YccFfa/` klasörüne kopyala.
4. Sunucuyu başlat. Ayar dosyası ilk açılışta `addons/counterstrikesharp/configs/plugins/YccFfa/YccFfa.json` olarak kendiliğinden oluşur; düzenleyip sunucuyu yeniden başlat.

## Komutlar

Sohbette `!` ile, konsolda `css_` ile yazılır.

| Komut | Ne yapar |
|---|---|
| `!tur` | Şu anki silah turu ve kalan süre |
| `!turdegis [numara]` | Admin: turu değiştirir (numara yoksa sıradaki) |

## Notlar

Sunucu deathmatch modunda açılmalı: `+game_type 1 +game_mode 2`. `cfg/ffa.cfg` (herkes herkese, satın alma kapalı, bedava zırh, sınırsız yedek mermi, botlar) ve `cfg/gamemode_deathmatch_server.cfg` dosyalarını sunucunun `game/csgo/cfg/` klasörüne kopyala; ikincisi oyunun kendi deathmatch ayarlarından sonra çalışıp bizimkilerin ezilmesini önler. Turlar, süreler, can ödülü, seri duyuruları ve harita listesi ayar dosyasından değişir.

## Lisans

MIT. Kullanabilir, değiştirebilir, paylaşabilirsin; lisans dosyasını koruman yeterli.

Sunucu: **yccteam.com.tr** · Site: https://yccteam.com.tr
