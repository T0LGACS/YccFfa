using YccFfa;
int hata = 0;
void Esit(object b, object g, string ad) { if (!Equals(b, g)) { hata++; Console.WriteLine($"YANLIS {ad}: beklenen {b}, gelen {g}"); } }
var r = new Random(1);
// Karışık: art arda aynı tur yok, 8 turluk bir döngüde her tur bir kez
var sira = new List<int>(); var s = 0; var goruldu = new HashSet<int> { 0 };
for (var i = 0; i < 7; i++) { var y = TurSirasi.Sonraki(s, 8, true, sira, r); Esit(true, y != s, "art arda aynı değil"); goruldu.Add(y); s = y; }
Esit(8, goruldu.Count, "7 değişimde 8 turun hepsi");
for (var i = 0; i < 200; i++) { var y = TurSirasi.Sonraki(s, 8, true, sira, r); Esit(true, y != s && y >= 0 && y < 8, "uzun çalışmada geçerli"); s = y; }
Esit(1, TurSirasi.Sonraki(0, 3, false, new(), r), "sıralı 0->1");
Esit(0, TurSirasi.Sonraki(2, 3, false, new(), r), "sıralı başa döner");
Esit(0, TurSirasi.Sonraki(0, 1, true, new(), r), "tek tur");
Esit("2 dk 05 sn", TurSirasi.Sure(125), "süre dk");
Esit("45 sn", TurSirasi.Sure(45), "süre sn");
Console.WriteLine(hata == 0 ? "TAMAM: tüm denemeler geçti" : $"{hata} deneme YANLIS");
return hata;
