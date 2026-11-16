namespace YccFfa;

// Oyun motorundan bağımsız kısım (testte de kullanılır): tur sırası ve süre yazısı.
public static class TurSirasi
{
    // Karışık sırada bir sonraki tur: aynı tur art arda gelmez, bütün turlar dönmeden biri ikinci kez gelmez.
    public static int Sonraki(int simdiki, int sayi, bool karisik, List<int> kalanlar, Random r)
    {
        if (sayi <= 1) return 0;
        if (!karisik) return (simdiki + 1) % sayi;
        if (kalanlar.Count == 0) kalanlar.AddRange(Enumerable.Range(0, sayi).Where(i => i != simdiki));
        var i = r.Next(kalanlar.Count);
        var s = kalanlar[i];
        kalanlar.RemoveAt(i);
        return s;
    }

    public static string Sure(int saniye) => saniye >= 60 ? $"{saniye / 60} dk {saniye % 60:00} sn" : $"{saniye} sn";
}
