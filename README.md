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

Sunucu normal modda açılır (`+game_type 0 +game_mode 0`): herkes herkese, ölünce yeniden doğma ve rastgele doğma `cfg/ffa.cfg` ile gelir. Deathmatch modu (`1/2`) da çalışır ama doğunca 'satın alma menüsü / rastgele silahlar' yazısı gösterir. `cfg/ffa.cfg` ile `cfg/gamemode_casual_server.cfg` (deathmatch için `gamemode_deathmatch_server.cfg`) dosyalarını sunucunun `game/csgo/cfg/` klasörüne kopyala; ikincisi oyunun kendi mod ayarlarından sonra çalışıp bizimkilerin ezilmesini önler. Satın alma süresi açık ama silah alınamaz: kapalıyken botlar doğunca yerinde duruyor. Turlar, süreler, can ödülü, seri duyuruları ve harita listesi ayar dosyasından değişir.

## Lisans

MIT. Kullanabilir, değiştirebilir, paylaşabilirsin; lisans dosyasını koruman yeterli.

Sunucu: **yccteam.com.tr** · Site: https://yccteam.com.tr
