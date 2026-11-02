namespace FenerRunner
{
    public static class RunnerShopCatalog
    {
        public static string Name(RunnerUpgrade kind)
        {
            switch(kind){case RunnerUpgrade.ChickSense:return "CİVCİV DUYUSU";case RunnerUpgrade.RainVolley:return "BEREKETLİ YAĞMUR";
                case RunnerUpgrade.RhythmBonus:return "TRİBÜN RİTMİ";case RunnerUpgrade.TrailVolley:return "ALTIN PAS";case RunnerUpgrade.HenNest:return "BÜYÜK KÜMES";default:return "";}
        }
        public static string Description(RunnerUpgrade kind,int level=0)
        {
            switch(kind){case RunnerUpgrade.ChickSense:return "Şerit değiştirirken civciv toplama alanına her seviyede +6 cm.";
                case RunnerUpgrade.RainVolley:return "Civciv yağmurunun her dalgasına seviye başına +1 civciv.";
                case RunnerUpgrade.RhythmBonus:return "Ritim serisinin her adımına seviye başına +15 puan.";
                case RunnerUpgrade.TrailVolley:return "Altın pas yeteneğiyle şerit değiştirince seviye başına +1 civciv.";
                case RunnerUpgrade.HenNest:return "Tavuk bonusundan çıkan sürüye seviye başına +2 civciv.";default:return "";}
        }
        public static string Value(RunnerUpgrade kind,int level)
        {
            switch(kind){case RunnerUpgrade.ChickSense:return "+"+(level*6)+" cm";
                case RunnerUpgrade.RhythmBonus:return "+"+(level*15)+" puan";
                case RunnerUpgrade.HenNest:return (12+level*2)+" civciv";default:return "+"+level+" civciv";}
        }
        public static string PowerDescription(PowerupKind kind)
        {
            switch(kind){case PowerupKind.Dash:return "Daha uzun depar. Engeller hâlâ tehlikeli.";case PowerupKind.Flight:return "Kanat aç ve parkurun üstünden süzül.";
                case PowerupKind.Magnet:return "Yakındaki civcivleri kendine çek.";case PowerupKind.Shield:return "Tek darbeyi karşılayan kalkan daha uzun dayanır.";
                case PowerupKind.DoubleCoins:return "Topladığın her civciv iki kat değerli.";case PowerupKind.SlowMotion:return "Karar vermek için daha uzun odak süresi.";
                case PowerupKind.SuperJump:return "Güçlü sıçrayışı daha uzun kullan.";case PowerupKind.CoinRain:return "Önüne daha uzun süre civciv yağsın.";
                case PowerupKind.ScoreBoost:return "Kazanılan puanları ikiye katla.";case PowerupKind.FeverRush:return "Tribün coşkusunun ödül süresini uzat.";
                case PowerupKind.AirStep:return "Havadaki ikinci sıçrayışını güçlendir.";case PowerupKind.LaneSweep:return "Üç şeridin civcivlerini birlikte topla.";
                case PowerupKind.Rhythm:return "Temiz engel geçişleriyle puan serisi yap.";case PowerupKind.GoldenTrail:return "Şerit değiştir, çıkan civcivleri yakala.";
                case PowerupKind.Encore:return "Etkin güçlere ek süre. Her seviye +0,5 sn.";default:return "";}
        }
        public static string VehicleName(ChaseVehicleKind kind)
        {switch(kind){case ChaseVehicleKind.RangeRover:return "RANGE ROVER";case ChaseVehicleKind.SportCoupe:return "KADIKÖY GT";case ChaseVehicleKind.Trail4x4:return "KANARYA 4×4";default:return "";}}
        public static string VehicleDescription(ChaseVehicleKind kind)
        {switch(kind){case ChaseVehicleKind.RangeRover:return "Siyah lüks SUV · panoramik tavan";case ChaseVehicleKind.SportCoupe:return "Alçak spor gövde · çift egzoz · arka kanat";case ChaseVehicleKind.Trail4x4:return "Arazi gövdesi · tavan sepeti · koruma demiri";default:return "";}}
        public static string VehicleResource(ChaseVehicleKind kind)
        {return kind==ChaseVehicleKind.SportCoupe?"ChaseCoupe":kind==ChaseVehicleKind.Trail4x4?"ChaseOffroad":"ChaseSUV";}
    }
}
