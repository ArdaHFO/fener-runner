#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FenerRunner
{
    public sealed class MarketGarageChecksV20:MonoBehaviour
    {
        const string Flag="FenerRunner.MarketGarageV20";
        [Serializable] sealed class Report{public bool passed;public string utc;public string[] checks,errors;public string scope="Native Unity scene, isolated saved wallets, all power/permanent tiers and vehicle purchases, persistence, combined effects, actual meshes and retained chase motion. No APK or physical-device test.";}
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        readonly List<RunWallet> wallets=new List<RunWallet>();
        RunnerGame game;bool background;int savedBalance;
        [MenuItem("Fener Runner/120 - Verify market and garage v20")]
        public static void StartChecks(){SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Market and garage v20 checks").AddComponent<MarketGarageChecksV20>();}}
        IEnumerator Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;Application.logMessageReceived+=Log;
            savedBalance=PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0);
            game=FindFirstObjectByType<RunnerGame>();game.AutomatedTest=true;
            yield return null;
            try{Economy();Effects();Vehicles();}catch(Exception e){errors.Add(e.ToString());}
            Check(PlayerPrefs.GetInt(RunWallet.DefaultPrefix+"Coins",0)==savedBalance,"The real saved wallet is untouched by isolated tests");
            foreach(var wallet in wallets)wallet.DeleteIsolatedTestData();
            var report=new Report{passed=errors.Count==0,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(ReviewEvidence.FileFor(20,"../Validation/unity_market_garage_v20.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Market garage v20 "+(report.passed?"PASSED":"FAILED")+": "+string.Join("; ",errors));EditorApplication.isPlaying=false;
        }
        RunWallet Wallet(string suffix,bool forGame=false)
        {
            string prefix="FenerRunner.Test.MarketV20."+Guid.NewGuid().ToString("N")+"."+suffix+".";
            var wallet=forGame?game.UseWalletForTest(prefix):new RunWallet(prefix);wallets.Add(wallet);return wallet;
        }
        void Economy()
        {
            Check(RunnerShopCatalog.Name(RunnerUpgrade.ChickSense)=="C\u0130VC\u0130V DUYUSU"&&RunnerShopCatalog.VehicleName(ChaseVehicleKind.SportCoupe)=="KADIK\u00d6Y GT","Turkish catalog text retains its actual Unicode characters");
            string prefix="FenerRunner.Test.MarketV20.Legacy."+Guid.NewGuid().ToString("N")+".";
            PlayerPrefs.SetInt(prefix+"Coins",321);PlayerPrefs.SetInt(prefix+"Upgrade.0",2);PlayerPrefs.SetInt(prefix+"Upgrade.1",1);PlayerPrefs.SetInt(prefix+"Upgrade.2",3);
            var legacy=new RunWallet(prefix);wallets.Add(legacy);
            Check(legacy.Balance==321&&legacy.Level(PowerupKind.Dash)==2&&legacy.Level(PowerupKind.Flight)==1&&legacy.Level(PowerupKind.Magnet)==3,"Existing v19 currency and three upgrades migrate without changing their saved keys or levels");
            Check(legacy.OwnsVehicle(ChaseVehicleKind.RangeRover)&&!legacy.OwnsVehicle(ChaseVehicleKind.SportCoupe)&&legacy.EquippedVehicle==ChaseVehicleKind.RangeRover,"Old saves receive the default black SUV and no paid unlocks");
            var empty=Wallet("Empty");
            foreach(PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))Check(!empty.Purchase(kind)&&empty.Balance==0,"No overdraft on empty power purchase "+kind);
            foreach(RunnerUpgrade kind in Enum.GetValues(typeof(RunnerUpgrade)))Check(!empty.Purchase(kind)&&empty.Balance==0,"No overdraft on empty permanent purchase "+kind);
            Check(!empty.PurchaseVehicle(ChaseVehicleKind.SportCoupe)&&!empty.EquipVehicle(ChaseVehicleKind.SportCoupe),"An unowned car cannot be bought without funds or equipped");
            empty.Credit(-100);empty.Credit(0);Check(empty.Balance==0,"Negative and zero credits cannot mutate earned balance");
            empty.Credit(int.MaxValue);empty.Credit(1);Check(empty.Balance==int.MaxValue,"Credit overflow saturates without negative currency");
            Check(!empty.Purchase((PowerupKind)(-1))&&!empty.Purchase((PowerupKind)200)&&!empty.Purchase((RunnerUpgrade)200)&&!empty.PurchaseVehicle((ChaseVehicleKind)200)&&!empty.EquipVehicle((ChaseVehicleKind)(-1)),"Invalid IDs cannot spend, unlock or select data");
            legacy.Credit(20000);int starting=legacy.Balance,spent=0;
            foreach(PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))
            {
                while(legacy.Level(kind)<RunWallet.MaximumLevel){int cost=legacy.Cost(kind);float before=kind==PowerupKind.Encore?legacy.EncoreExtension:legacy.Duration(kind);bool bought=legacy.Purchase(kind);spent+=cost;float after=kind==PowerupKind.Encore?legacy.EncoreExtension:legacy.Duration(kind);Check(bought&&after>before,"Every purchased tier improves its actual effect "+kind+" tier"+legacy.Level(kind));}
                int balance=legacy.Balance;Check(legacy.Cost(kind)==0&&!legacy.Purchase(kind)&&legacy.Balance==balance,"Maximum tier does not debit repeated clicks "+kind);
            }
            foreach(RunnerUpgrade kind in Enum.GetValues(typeof(RunnerUpgrade)))
            {
                for(int tier=1;tier<=3;tier++){int cost=legacy.Cost(kind);spent+=cost;Check(legacy.Purchase(kind)&&legacy.Level(kind)==tier,"Permanent tier purchase "+kind+" "+tier);}
                int balance=legacy.Balance;Check(!legacy.Purchase(kind)&&legacy.Balance==balance,"Permanent maximum does not debit "+kind);
            }
            foreach(var kind in new[]{ChaseVehicleKind.SportCoupe,ChaseVehicleKind.Trail4x4})
            {
                spent+=RunWallet.VehiclePrice(kind);Check(legacy.PurchaseVehicle(kind)&&legacy.OwnsVehicle(kind)&&legacy.EquippedVehicle==kind,"Purchase owns and equips "+kind);
                int balance=legacy.Balance;Check(!legacy.PurchaseVehicle(kind)&&legacy.Balance==balance,"Repeated vehicle purchase never double-debits "+kind);
            }
            Check(legacy.Balance==starting-spent,"All purchases debit exactly their advertised integer costs");
            var restored=new RunWallet(prefix);bool equal=restored.Balance==legacy.Balance&&restored.EquippedVehicle==ChaseVehicleKind.Trail4x4;
            foreach(PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))equal&=restored.Level(kind)==3;
            foreach(RunnerUpgrade kind in Enum.GetValues(typeof(RunnerUpgrade)))equal&=restored.Level(kind)==3;
            foreach(ChaseVehicleKind kind in Enum.GetValues(typeof(ChaseVehicleKind)))equal&=restored.OwnsVehicle(kind);
            Check(equal,"Every purchased level, all three cars, equipped car and exact balance survive save reload");
            PlayerPrefs.SetInt(prefix+"Vehicle.Selected",99);Check(new RunWallet(prefix).EquippedVehicle==ChaseVehicleKind.RangeRover,"Corrupt selected ID falls back to owned default");
            PlayerPrefs.SetInt(prefix+"Vehicles",1);PlayerPrefs.SetInt(prefix+"Vehicle.Selected",1);Check(new RunWallet(prefix).EquippedVehicle==ChaseVehicleKind.RangeRover,"A stale selected value cannot equip an unowned paid car");
        }
        void Effects()
        {
            game.ReturnToMenu();var wallet=Wallet("Effects",true);wallet.Credit(20000);
            foreach(RunnerUpgrade kind in Enum.GetValues(typeof(RunnerUpgrade)))for(int i=0;i<3;i++)wallet.Purchase(kind);
            for(int i=0;i<3;i++)wallet.Purchase(PowerupKind.Encore);
            game.Play();game.enabled=false;game.Obstacles.SetActive(false);game.Tokens.Enabled=true;game.Powerups.IsolatePickupsForTest();
            var powers=game.Powerups;int bonus=game.Tokens.BonusSpawned;
            powers.Activate(PowerupKind.CoinRain);powers.TickTimers(1.01f);
            Check(game.Tokens.BonusSpawned-bonus==8,"Fully upgraded rain emits eight actual pooled chicks per beat");
            powers.ResetCourse();powers.IsolatePickupsForTest();bonus=game.Tokens.BonusSpawned;
            powers.Activate(PowerupKind.GoldenTrail);powers.SuccessfulLaneChange();
            Check(game.Tokens.BonusSpawned-bonus==7,"Fully upgraded trail creates seven actual pooled chicks");
            int before=game.Tokens.BonusSpawned;powers.SuccessfulLaneChange();Check(game.Tokens.BonusSpawned==before,"Rapid repeated callbacks do not bypass the trail reward cooldown");
            powers.ResetCourse();powers.IsolatePickupsForTest();powers.Activate(PowerupKind.Rhythm);
            Check(powers.RewardCleanPasses(1)==95&&powers.RewardCleanPasses(1)==190,"Permanent rhythm upgrade changes real ascending score rewards");
            powers.ResetCourse();powers.IsolatePickupsForTest();powers.Activate(PowerupKind.Dash);float remaining=powers.DashRemaining;powers.Activate(PowerupKind.Encore);
            Check(Mathf.Abs(powers.DashRemaining-remaining-3.5f)<.001f,"Upgraded Encore gives a real 3.5-second extension");
            for(int i=0;i<20;i++)powers.Activate(PowerupKind.Encore);
            Check(powers.DashRemaining<=game.PowerupDuration(PowerupKind.Dash)+3.501f,"Repeated upgraded Encore remains bounded");
            powers.ResetCourse();powers.IsolatePickupsForTest();powers.Activate(PowerupKind.Magnet);bonus=game.Tokens.BonusSpawned;
            powers.RewardCleanPasses(1);Check(game.Tokens.BonusSpawned==bonus&&!powers.FlockComboActive,"One power alone cannot trigger the combined flock reward");
            powers.Activate(PowerupKind.DoubleCoins);int coins=game.Coins;powers.RewardCleanPasses(1);
            Check(powers.FlockComboActive&&powers.FlockComboVolleys==1&&game.Tokens.BonusSpawned-bonus==3&&game.Coins==coins,"Magnet plus double-chicks creates three collectible chicks on a clean pass without auto-crediting them");
            Check(!powers.IsProtected&&!powers.TryAbsorbHit(),"The new combo and permanent rewards provide no hidden invulnerability");
            powers.Activate(PowerupKind.Shield);powers.TryAbsorbHit();bonus=game.Tokens.BonusSpawned;powers.RewardCleanPasses(1);
            Check(game.Tokens.BonusSpawned==bonus,"An absorbed collision spoils that row's flock-combo reward");
            game.Restart();Check(!powers.FlockComboActive&&powers.FlockComboVolleys==0,"Restart clears transient combo state while preserving bought upgrades");
            game.ReturnToMenu();game.enabled=true;
        }
        void Vehicles()
        {
            var chase=game.Chase;
            Check(chase.RetainedVehicleCount==3,"All three chase models are loaded once into a retained pool");
            int count=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            var shapes=new HashSet<Mesh>();float[] heights=new float[3];
            foreach(ChaseVehicleKind kind in Enum.GetValues(typeof(ChaseVehicleKind)))
            {
                Check(chase.ApplySelection(kind)&&chase.SelectedVehicle==kind,"Actual chase selection changes to "+kind);
                var visual=chase.VisualRoot;var coach=visual.Find("Suspension/Coachwork").GetComponent<MeshFilter>();shapes.Add(coach.sharedMesh);heights[(int)kind]=coach.sharedMesh.bounds.size.y;
                var filters=visual.GetComponentsInChildren<MeshFilter>(true);var renderers=visual.GetComponentsInChildren<MeshRenderer>(true);
                Check(visual.GetComponentsInChildren<Collider>(true).Length==0&&filters.Sum(f=>f.sharedMesh.triangles.Length/3)<40000&&renderers.Length==6,"Visual-only bounded six-renderer mesh model "+kind);
                Check(filters.All(f=>f.sharedMesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)))&&renderers.All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)),"Finite geometry and supported native materials "+kind);
                var preview=Resources.Load<Sprite>("VehiclePreview_"+kind);Check(preview!=null&&preview.rect.width==760&&preview.rect.height==300,"Real mesh shop preview baked for "+kind);
                game.Play();game.enabled=false;chase.ApplySelection(kind);game.Motor.transform.position=new Vector3(0,0,100);
                bool visible=false;float minimum=1000;
                for(int i=0;i<600;i++){game.Motor.transform.position+=Vector3.forward*.33f;chase.Tick(1f/60);if(chase.IsVisible){visible=true;minimum=Mathf.Min(minimum,game.Motor.transform.position.z-chase.VisualRoot.position.z-ChaseSUV.VehicleHalfLength);}}
                Check(visible&&minimum>1.2f,"Animated approach remains behind the runner "+kind);
                game.Pause();float clock=chase.PresentationTime;Vector3 position=chase.VisualRoot.position;chase.Tick(1);Check(chase.PresentationTime==clock&&chase.VisualRoot.position==position,"Pause freezes selected car animation "+kind);
                game.ReturnToMenu();chase.Tick(.1f);Check(!chase.IsVisible,"Return to menu hides selected car "+kind);game.enabled=true;
            }
            Check(shapes.Count==3&&heights[1]<heights[0]-.25f&&heights[2]>heights[1]+.5f,"Three separately authored body meshes have distinct coupe/SUV/4x4 silhouettes");
            for(int i=0;i<50;i++)chase.ApplySelection((ChaseVehicleKind)(i%3));
            Check(FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==count,"Repeated garage equip and running cycles never grow the vehicle pool");
            Check(!chase.ApplySelection((ChaseVehicleKind)99),"Invalid visual selection is rejected");chase.ApplySelection(ChaseVehicleKind.RangeRover);
        }
        void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)errors.Add(label);}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        void OnDestroy(){Application.logMessageReceived-=Log;Application.runInBackground=background;if(game!=null)game.enabled=true;}
    }
}
#endif
