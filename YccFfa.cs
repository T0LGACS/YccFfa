using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

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
        new() { Ad = "AK-47", Birincil = "weapon_ak47", Ikincil = "weapon_deagle" },
        new() { Ad = "M4A1-S", Birincil = "weapon_m4a1_silencer", Ikincil = "weapon_usp_silencer" },
        new() { Ad = "AWP", Birincil = "weapon_awp", Ikincil = "weapon_deagle" },
        new() { Ad = "M4A4", Birincil = "weapon_m4a1", Ikincil = "weapon_glock" },
        new() { Ad = "Sadece Deagle", Birincil = "", Ikincil = "weapon_deagle" },
        new() { Ad = "SSG 08 (Scout)", Birincil = "weapon_ssg08", Ikincil = "weapon_p250" },
        new() { Ad = "SMG (MP9)", Birincil = "weapon_mp9", Ikincil = "weapon_tec9" },
        new() { Ad = "Galil / FAMAS", Birincil = "weapon_galilar", Ikincil = "weapon_fiveseven" },
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
            return HookResult.Continue;
        });

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

    private void DogusSilahi(CCSPlayerController p)
    {
        var ws = p.IsValid && p.PawnIsAlive ? p.PlayerPawn.Value?.WeaponServices : null;
        if (ws == null) return;
        var t = Config.Turlar[_tur];
        var var_ = ws.MyWeapons.Select(h => h.Value).Where(w => w != null && w.IsValid).Select(w => w!.DesignerName).ToHashSet();
        if (t.Ikincil != "" && !var_.Contains(t.Ikincil)) p.GiveNamedItem(t.Ikincil);
        if (t.Birincil == "" || var_.Contains(t.Birincil)) return;
        p.GiveNamedItem(t.Birincil);
        if (!p.IsBot) AddTimer(0.1f, () => { if (p.IsValid && p.PawnIsAlive) p.ExecuteClientCommand("slot1"); });
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
