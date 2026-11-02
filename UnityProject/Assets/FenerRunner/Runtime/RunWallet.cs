using System;
using UnityEngine;

namespace FenerRunner
{
    // IDs are persistent. Existing Coins and Upgrade.0..2 keys keep their meaning.
    public enum RunnerUpgrade { ChickSense, RainVolley, RhythmBonus, TrailVolley, HenNest }
    public enum ChaseVehicleKind { RangeRover, SportCoupe, Trail4x4 }

    public sealed class RunWallet
    {
        public const string DefaultPrefix="FenerRunner.Progress.";
        public const int MaximumLevel=3, PermanentUpgradeCount=5, VehicleCount=3;
        static readonly int[] costs={30,75,150};
        static readonly int[] permanentCosts={60,140,280};
        readonly string prefix;
        readonly int[] levels=new int[RunnerPowerups.TypeCount],permanent=new int[PermanentUpgradeCount];
        int ownedVehicles=1;
        public int Balance{get;private set;}
        public ChaseVehicleKind EquippedVehicle{get;private set;}
        public bool IsIsolated=>prefix!=DefaultPrefix;
        public RunWallet(string storagePrefix=DefaultPrefix)
        {
            if(string.IsNullOrEmpty(storagePrefix))throw new ArgumentException("A storage prefix is required.");
            prefix=storagePrefix;
            Balance=Math.Max(0,PlayerPrefs.GetInt(prefix+"Coins",0));
            for(int i=0;i<levels.Length;i++)levels[i]=Mathf.Clamp(PlayerPrefs.GetInt(prefix+"Upgrade."+i,0),0,MaximumLevel);
            for(int i=0;i<permanent.Length;i++)permanent[i]=Mathf.Clamp(PlayerPrefs.GetInt(prefix+"Permanent."+i,0),0,MaximumLevel);
            ownedVehicles=(PlayerPrefs.GetInt(prefix+"Vehicles",1)&7)|1;
            var selected=(ChaseVehicleKind)PlayerPrefs.GetInt(prefix+"Vehicle.Selected",0);
            EquippedVehicle=OwnsVehicle(selected)?selected:ChaseVehicleKind.RangeRover;
        }
        public int Level(PowerupKind kind){int i=(int)kind;return i>=0&&i<levels.Length?levels[i]:0;}
        public int Cost(PowerupKind kind)
        {int i=(int)kind;return i>=0&&i<levels.Length&&levels[i]<MaximumLevel?costs[levels[i]]:0;}
        public bool Purchase(PowerupKind kind)
        {
            int cost=Cost(kind);if(cost<=0||Balance<cost)return false;
            Balance-=cost;levels[(int)kind]++;Save();return true;
        }
        public int Level(RunnerUpgrade kind){int i=(int)kind;return i>=0&&i<permanent.Length?permanent[i]:0;}
        public int Cost(RunnerUpgrade kind)
        {int i=(int)kind;return i>=0&&i<permanent.Length&&permanent[i]<MaximumLevel?permanentCosts[permanent[i]]:0;}
        public bool Purchase(RunnerUpgrade kind)
        {
            int cost=Cost(kind);if(cost<=0||Balance<cost)return false;
            Balance-=cost;permanent[(int)kind]++;Save();return true;
        }
        public bool OwnsVehicle(ChaseVehicleKind kind)
        {int i=(int)kind;return i>=0&&i<VehicleCount&&(ownedVehicles&(1<<i))!=0;}
        public static int VehiclePrice(ChaseVehicleKind kind)
        {return kind==ChaseVehicleKind.SportCoupe?450:kind==ChaseVehicleKind.Trail4x4?700:0;}
        public bool PurchaseVehicle(ChaseVehicleKind kind)
        {
            int cost=VehiclePrice(kind);if(cost<=0||OwnsVehicle(kind)||Balance<cost)return false;
            Balance-=cost;ownedVehicles|=1<<(int)kind;EquippedVehicle=kind;Save();return true;
        }
        public bool EquipVehicle(ChaseVehicleKind kind)
        {if(!OwnsVehicle(kind))return false;EquippedVehicle=kind;Save();return true;}
        public void Credit(int coins)
        {if(coins<=0)return;Balance=(int)Math.Min(int.MaxValue,(long)Balance+coins);Save();}
        public float Duration(PowerupKind kind)
        {
            float duration;
            switch(kind)
            {
                case PowerupKind.Dash:case PowerupKind.Flight:duration=5;break;
                case PowerupKind.Magnet:case PowerupKind.CoinRain:duration=6;break;
                case PowerupKind.Shield:case PowerupKind.DoubleCoins:case PowerupKind.ScoreBoost:case PowerupKind.FeverRush:duration=8;break;
                case PowerupKind.SlowMotion:case PowerupKind.LaneSweep:duration=5;break;
                case PowerupKind.SuperJump:case PowerupKind.AirStep:case PowerupKind.GoldenTrail:duration=7;break;
                case PowerupKind.Rhythm:duration=9;break;
                // Encore is a one-shot extension, so its upgrade extends the other powers,
                // not an otherwise meaningless HUD timer.
                case PowerupKind.Encore:return 1.2f;
                default:return 0;
            }
            return duration+Level(kind);
        }
        public float EncoreExtension=>2f+.5f*Level(PowerupKind.Encore);
        void Save()
        {
            PlayerPrefs.SetInt(prefix+"Coins",Balance);
            for(int i=0;i<levels.Length;i++)PlayerPrefs.SetInt(prefix+"Upgrade."+i,levels[i]);
            for(int i=0;i<permanent.Length;i++)PlayerPrefs.SetInt(prefix+"Permanent."+i,permanent[i]);
            PlayerPrefs.SetInt(prefix+"Vehicles",ownedVehicles);PlayerPrefs.SetInt(prefix+"Vehicle.Selected",(int)EquippedVehicle);
            PlayerPrefs.Save();
        }
#if UNITY_EDITOR
        public void DeleteIsolatedTestData()
        {
            if(!prefix.StartsWith("FenerRunner.Test.",StringComparison.Ordinal))throw new InvalidOperationException("Refusing to delete a real wallet");
            PlayerPrefs.DeleteKey(prefix+"Coins");
            for(int i=0;i<levels.Length;i++)PlayerPrefs.DeleteKey(prefix+"Upgrade."+i);
            for(int i=0;i<permanent.Length;i++)PlayerPrefs.DeleteKey(prefix+"Permanent."+i);
            PlayerPrefs.DeleteKey(prefix+"Vehicles");PlayerPrefs.DeleteKey(prefix+"Vehicle.Selected");PlayerPrefs.Save();
        }
#endif
    }
}
