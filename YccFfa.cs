using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.UserMessages;

namespace YccFfa;

// FFA (herkes herkese) sunucusu (sunucu sahibi 2026-11-16: "ffa sunucusu olacak arada silah değişimleri olacak").
// Oyun deathmatch modunda, takım arkadaşı da düşman (cfg/ffa.cfg). Bu eklenti:
// - silah turları: her TurSuresi saniyede bir tur değişir, herkese o turun silahları verilir (doğunca da onlar gelir),
// - değişimden UyariSuresi saniye önce ve değişince ekranda/sohbette duyuru,
// - öldürünce can ve şarjör yenileme, öldürme serisi duyuruları, !tur komutu.
public class Tur
{
    [JsonPropertyName("Ad")] public string Ad { get; set; } = "";
    [JsonPropertyName("Birincil")] public string Birincil { get; set; } = "";   // boşsa sadece tabanca turu
    [JsonPropertyName("Ikincil")] public string Ikincil { get; set; } = "";
}

public class Ayarlar : BasePluginConfig
{
    [JsonPropertyName("TurSuresi")] public int TurSuresi { get; set; } = 180;
    [JsonPropertyName("UyariSuresi")] public int UyariSuresi { get; set; } = 10;
    [JsonPropertyName("Karisik")] public bool Karisik { get; set; } = true;
    // Doğduktan sonra hasar almama süresi (sn). Oyunun kendi korumasını (mp_respawn_immunitytime) kullanmıyoruz: o açıkken
    // ekranda "B: satın alma menüsü / F3: rastgele silahlar" yazısı çıkıyor (sunucu sahibi 2026-11-16)
    [JsonPropertyName("KorumaSuresi")] public float KorumaSuresi { get; set; } = 0f;   // 0: oyunun kendi koruması kullanılır (mp_respawn_immunitytime)
    [JsonPropertyName("OldurunceCan")] public int OldurunceCan { get; set; } = 25;
    [JsonPropertyName("OldurunceSarjor")] public bool OldurunceSarjor { get; set; } = true;
    [JsonPropertyName("Seriler")] public List<int> Seriler { get; set; } = new() { 3, 5, 10, 15, 20 };
    // Harita süresi (mp_timelimit) bitince sıradaki haritaya geçilir; boşsa aynı harita yeniden başlar
    [JsonPropertyName("Haritalar")] public List<string> Haritalar { get; set; } = new() { "de_mirage", "de_dust2", "de_inferno", "de_ancient", "de_anubis", "de_nuke", "de_cache", "de_train" };
    [JsonPropertyName("HaritaGecisSuresi")] public float HaritaGecisSuresi { get; set; } = 12;
    [JsonPropertyName("AdminYetki")] public string AdminYetki { get; set; } = "@css/changemap";
    [JsonPropertyName("On")] public string On { get; set; } = "{GOLD}[FFA]{DEFAULT}";
    [JsonPropertyName("Turlar")] public List<Tur> Turlar { get; set; } = new()
    {
        // Her turda tek silah (sunucu sahibi 2026-11-16: "AK raundunda tabancayı silmiyor"); Ikincil boşsa tabanca alınır
        new() { Ad = "AK-47", Birincil = "weapon_ak47" },
        new() { Ad = "M4A1-S", Birincil = "weapon_m4a1_silencer" },
        new() { Ad = "AWP", Birincil = "weapon_awp" },
        new() { Ad = "M4A4", Birincil = "weapon_m4a1" },
        new() { Ad = "Sadece Deagle", Birincil = "", Ikincil = "weapon_deagle" },
        new() { Ad = "SSG 08 (Scout)", Birincil = "weapon_ssg08" },
        new() { Ad = "Galil", Birincil = "weapon_galilar" },
    };
}

public class YccFfa : BasePlugin, IPluginConfig<Ayarlar>
{
    public override string ModuleName => "YccFfa";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "YCC TEAM";
    public override string ModuleDescription => "FFA: silah turları, öldürme ödülü, seri duyuruları";

    public Ayarlar Config { get; set; } = new();
    public void OnConfigParsed(Ayarlar c)
    {
        c.Turlar = c.Turlar.Where(t => t.Ad != "" && (t.Birincil != "" || t.Ikincil != "")).ToList();
        if (c.Turlar.Count == 0) c.Turlar = new Ayarlar().Turlar;
        c.TurSuresi = Math.Max(30, c.TurSuresi);
        c.UyariSuresi = Math.Clamp(c.UyariSuresi, 0, c.TurSuresi - 5);
        Config = c;
    }

    private int _tur;
    private int _kalan;
    private readonly List<int> _sira = new();
    private readonly Random _r = new();
    private readonly Dictionary<int, int> _seri = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _saat;

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnMapStart>(_ => AddTimer(1f, () => Baslat(_r.Next(Config.Turlar.Count))));
        RegisterListener<Listeners.OnClientDisconnect>(s => _seri.Remove(s));
        RegisterEventHandler<EventPlayerDeath>(Olum);
        RegisterEventHandler<EventCsWinPanelMatch>((_, _) => { HaritaSonu(); return HookResult.Continue; });
        // Deathmatch modunda oyun doğuşta sadece tabancayı veriyor (mp_*_default_primary'yi yok sayıyor, 2026-11-16 görüldü):
        // turun ana silahı doğduktan hemen sonra eklentiden verilir
        RegisterEventHandler<EventPlayerSpawn>((e, _) =>
        {
            if (e.Userid is not { IsValid: true } p) return HookResult.Continue;
            _seri.Remove(p.Slot);
            AddTimer(0.2f, () => DogusSilahi(p));
            if (Config.KorumaSuresi > 0) Server.NextFrame(() =>
            {
                if (p.PlayerPawn.Value is not { IsValid: true } pw) return;
                pw.TakesDamage = false;
                AddTimer(Config.KorumaSuresi, () => { if (pw.IsValid) pw.TakesDamage = true; });
            });
            return HookResult.Continue;
        });

        // Oyuncular satın alma menüsünü (rastgele silah dahil) açamaz (sunucu sahibi 2026-11-16: "rastgele satın almayı kapat").
        // Satın alma süresi yine de açık kalır: kapalıyken botlar doğunca alışveriş bekleyip yerinde duruyordu.
        foreach (var k in new[] { "buymenu", "buy", "autobuy", "rebuy", "buyrandom" })
            AddCommandListener(k, (p, _) => p != null && p.IsValid && !p.IsBot ? HookResult.Handled : HookResult.Continue);

        // Deathmatch'in "X ile bir düşman öldürdüğün için N puan" yazıları gösterilmez (sunucu sahibi 2026-11-16: "bu yazıları kaldır").
        // Oyun bunları TextMsg ile yolluyor; puan ödülü anahtarları yakalanıp durdurulur, diğer yazılar geçer.
        // Sunucu açılırken mesaj tablosu henüz hazır olmayabiliyor ("Could not find user message", 2026-11-16): olmazsa harita başlayınca
        PuanYazilariniKapat();
        RegisterListener<Listeners.OnMapStart>(_ => PuanYazilariniKapat());

        AddCommand("css_tur", "FFA: şu anki silah turu ve kalan süre", (p, info) =>
        {
            var t = Config.Turlar[_tur];
            var m = $"Şu anki tur: {{GREEN}}{t.Ad}{{DEFAULT}} · kalan {{GOLD}}{TurSirasi.Sure(_kalan)}";
            if (p == null) info.ReplyToCommand(ChatRenk.Temizle(m)); else Yaz(p, m);
        });
        // Sadece konsol: botların yeri, hızı ve hareket türü (botlar hareket etmiyor şikâyeti, 2026-11-16)
        AddCommand("css_ffabot", "Konsol: bot durumu", (p, info) =>
        {
            if (p != null) return;
            foreach (var b in Utilities.GetPlayers().Where(x => x.IsValid && x.IsBot))
            {
                var pw = b.PlayerPawn.Value;
                var o = pw?.AbsOrigin; var v = pw?.AbsVelocity;
                info.ReplyToCommand($"{b.PlayerName} takim={b.TeamNum} canli={b.PawnIsAlive} yer=({o?.X:0},{o?.Y:0},{o?.Z:0}) hiz={(v == null ? 0 : Math.Sqrt(v.X * v.X + v.Y * v.Y)):0} hareket={pw?.MoveType} can={pw?.Health} silah={pw?.WeaponServices?.ActiveWeapon.Value?.DesignerName}");
            }
        });
        AddCommand("css_turdegis", "Admin: silah turunu değiştirir (numara verilmezse sıradaki)", (p, info) =>
        {
            if (p != null && !AdminManager.PlayerHasPermissions(p, Config.AdminYetki)) { Yaz(p, "{RED}Yetkin yok."); return; }
            var sonraki = int.TryParse(info.GetArg(1), out var n) && n >= 1 && n <= Config.Turlar.Count ? n - 1
                : TurSirasi.Sonraki(_tur, Config.Turlar.Count, Config.Karisik, _sira, _r);
            Baslat(sonraki);
        });
        if (hotReload) Baslat(0);
    }

    // Yeni tur: doğunca gelecek silahlar (mp_*_default_*) ayarlanır, hayattakilerin silahları değişir
    private void Baslat(int tur)
    {
        _tur = tur;
        _kalan = Config.TurSuresi;
        _sonraki = null;
        var t = Config.Turlar[tur];
        foreach (var takim in new[] { "t", "ct" })
        {
            Server.ExecuteCommand($"mp_{takim}_default_primary \"{t.Birincil}\"");
            Server.ExecuteCommand($"mp_{takim}_default_secondary \"{t.Ikincil}\"");
        }
        // Botların silahı da değişir (2026-11-16: FFA'da boşluğu botlar dolduruyor)
        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p.PawnIsAlive)) SilahVer(p, t);
        HerkeseYaz($"Yeni tur: {{GREEN}}{t.Ad}{{DEFAULT}} ({TurSirasi.Sure(Config.TurSuresi)})");
        foreach (var p in Oyuncular()) p.PrintToCenter($"Yeni tur: {t.Ad}");
        _saat?.Kill();
        _saat = AddTimer(1f, Saniye, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    // Harita bitince sıradaki haritaya geçer (sırada şu anki harita varsa ondan sonrakine)
    private void HaritaSonu()
    {
        var l = Config.Haritalar.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (l.Count == 0) return;
        _saat?.Kill();
        var i = l.IndexOf(Server.MapName);
        var sonraki = l[(i + 1) % l.Count];
        if (sonraki == Server.MapName && l.Count > 1) sonraki = l[(i + 2) % l.Count];
        HerkeseYaz($"Harita bitti! Sıradaki harita: {{GREEN}}{sonraki}");
        AddTimer(Config.HaritaGecisSuresi, () => Server.ExecuteCommand($"changelevel {sonraki}"), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void Saniye()
    {
        _kalan--;
        if (_kalan == Config.UyariSuresi && Config.UyariSuresi > 0)
        {
            // Sıradaki tur şimdiden seçilir ki duyuruda adı yazsın
            var s = TurSirasi.Sonraki(_tur, Config.Turlar.Count, Config.Karisik, _sira, _r);
            _sonraki = s;
            HerkeseYaz($"{{GOLD}}{Config.UyariSuresi} sn{{DEFAULT}} sonra yeni tur: {{GREEN}}{Config.Turlar[s].Ad}");
        }
        if (_kalan > 0) return;
        Baslat(_sonraki ?? TurSirasi.Sonraki(_tur, Config.Turlar.Count, Config.Karisik, _sira, _r));
    }
    private int? _sonraki;

    // Önce bıçağa geçilir, silahlar sonra alınır: eldeki silahı o anda silmek oyuncunun oyununda sorun çıkarabiliyor
    // (YccSkin'de 2026-11-16 görüldü). Bıçağa dokunulmaz.
    private void SilahVer(CCSPlayerController p, Tur t)
    {
        if (!p.IsBot) p.ExecuteClientCommand("slot3");
        AddTimer(0.15f, () =>
        {
            var ws = p.IsValid && p.PawnIsAlive ? p.PlayerPawn.Value?.WeaponServices : null;
            if (ws == null) return;
            var silahlar = ws.MyWeapons.Select(h => h.Value).Where(w => w != null && w.IsValid).Select(w => w!.DesignerName)
                .Where(ad => !ad.Contains("knife") && !ad.Contains("bayonet")).Distinct().ToList();
            foreach (var ad in silahlar) p.RemoveItemByDesignerName(ad, true);
            if (t.Ikincil != "") p.GiveNamedItem(t.Ikincil);
            if (t.Birincil != "") p.GiveNamedItem(t.Birincil);
            if (!p.IsBot) AddTimer(0.1f, () => { if (p.IsValid && p.PawnIsAlive) p.ExecuteClientCommand(t.Birincil != "" ? "slot1" : "slot2"); });
        });
    }

    private bool _mesajKancasi;
    private void PuanYazilariniKapat()
    {
        if (_mesajKancasi) return;
        try
        {
            int id;
            try { id = UserMessage.FindIdByName("CUserMessageTextMsg"); } catch { id = 124; }
            HookUserMessage(id > 0 ? id : 124, um =>
            {
                if (um.GetRepeatedFieldCount("param") == 0) return HookResult.Continue;
                var anahtar = um.ReadString("param", 0);
                if (PuanYazisi(anahtar)) return HookResult.Stop;
                if (anahtar.StartsWith("#") && _gorulen.Add(anahtar)) Console.WriteLine("[YccFfa] yazı geçti: " + anahtar);   // shortcut: teşhis, yazılar oturunca silinecek
                return HookResult.Continue;
            }, HookMode.Pre);
            _mesajKancasi = true;
        }
        catch (Exception e) { Console.WriteLine("[YccFfa] mesaj kancası kurulamadı, harita başlayınca denenecek: " + e.Message); }
    }

    private readonly HashSet<string> _gorulen = new();
    public static bool PuanYazisi(string anahtar) =>
        System.Text.RegularExpressions.Regex.IsMatch(anahtar, "Point_Award|BonusPoints|Bonus_Points|DM_Kill|dm_.*points|Cannot_Carry_Anymore", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // Silahlar yuvasına göre bakılır: M4A1-S / USP-S gibi silahların sunucudaki adı farklı olabiliyor, adla bakınca
    // ikinci kez verilip "daha fazla taşıyamazsın" çıkıyordu. Doğunca ana silah ele alınır (oyun tabancayla doğuruyor).
    private void DogusSilahi(CCSPlayerController p)
    {
        var ws = p.IsValid && p.PawnIsAlive ? p.PlayerPawn.Value?.WeaponServices : null;
        if (ws == null) return;
        var t = Config.Turlar[_tur];
        var yuvalar = ws.MyWeapons.Select(h => h.Value).Where(w => w != null && w.IsValid)
            .Select(w => w!.GetVData<CCSWeaponBaseVData>()?.GearSlot).ToList();
        if (t.Ikincil != "" && !yuvalar.Contains(gear_slot_t.GEAR_SLOT_PISTOL)) p.GiveNamedItem(t.Ikincil);
        if (t.Ikincil == "") TabancalariAl(p, ws);
        if (t.Birincil == "") return;
        if (!yuvalar.Contains(gear_slot_t.GEAR_SLOT_RIFLE)) p.GiveNamedItem(t.Birincil);
        if (!p.IsBot) AddTimer(0.3f, () => { if (p.IsValid && p.PawnIsAlive) p.ExecuteClientCommand("slot1"); });
    }

    // Tek silahlı turda tabanca(lar) alınır (deathmatch doğuşta tabanca veriyor)
    private static void TabancalariAl(CCSPlayerController p, CPlayer_WeaponServices ws)
    {
        var tabancalar = ws.MyWeapons.Select(h => h.Value).Where(w => w != null && w.IsValid && w!.GetVData<CCSWeaponBaseVData>()?.GearSlot == gear_slot_t.GEAR_SLOT_PISTOL)
            .Select(w => w!.DesignerName).Distinct().ToList();
        foreach (var ad in tabancalar) p.RemoveItemByDesignerName(ad, true);
    }

    private HookResult Olum(EventPlayerDeath e, GameEventInfo _)
    {
        var olen = e.Userid;
        if (olen != null) _seri.Remove(olen.Slot);
        var o = e.Attacker;
        if (o == null || !o.IsValid || o == olen || o.IsBot || !o.PawnIsAlive) return HookResult.Continue;
        var pawn = o.PlayerPawn.Value;
        if (pawn == null) return HookResult.Continue;

        if (Config.OldurunceCan > 0 && pawn.Health < 100)
        {
            pawn.Health = Math.Min(100, pawn.Health + Config.OldurunceCan);
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
        if (Config.OldurunceSarjor && pawn.WeaponServices?.ActiveWeapon.Value is { IsValid: true } w)
        {
            var v = w.GetVData<CBasePlayerWeaponVData>();
            if (v != null && v.MaxClip1 > 0) { w.Clip1 = v.MaxClip1; Utilities.SetStateChanged(w, "CBasePlayerWeapon", "m_iClip1"); }
        }
        var seri = _seri[o.Slot] = (_seri.TryGetValue(o.Slot, out var s) ? s : 0) + 1;
        if (Config.Seriler.Contains(seri)) HerkeseYaz($"{{LIGHTRED}}{o.PlayerName}{{DEFAULT}} üst üste {{GOLD}}{seri}{{DEFAULT}} kişiyi indirdi!");
        return HookResult.Continue;
    }

    private static IEnumerable<CCSPlayerController> Oyuncular() =>
        Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV);

    private string Renk(string s) => " " + ChatRenk.Uygula(Config.On + " " + s);
    private void Yaz(CCSPlayerController p, string s) => p.PrintToChat(Renk(s));
    private void HerkeseYaz(string s) => Server.PrintToChatAll(Renk(s));
}

// {GREEN} gibi renk adlarını sohbet renklerine çevirir
public static class ChatRenk
{
    public static string Uygula(string s)
    {
        foreach (var f in typeof(ChatColors).GetFields())
            if (f.GetValue(null) is char c) s = s.Replace("{" + f.Name.ToUpperInvariant() + "}", c.ToString());
        return s;
    }

    // Konsola giden yazıda renk adları görünmesin
    public static string Temizle(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\{[A-Z]+\}", "");
}
