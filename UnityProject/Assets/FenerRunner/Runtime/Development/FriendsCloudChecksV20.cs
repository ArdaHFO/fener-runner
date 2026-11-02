#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;

namespace FenerRunner
{
    public sealed class FriendsCloudChecksV20:MonoBehaviour
    {
        const string Flag="FenerRunner.FriendsCloudChecksV20",ProfileA="fener-v20-friends-a",ProfileB="fener-v20-friends-b";
        [Serializable] sealed class Report
        {
            public bool passed,actualCloud,preferencesPreserved,defaultServicesPreserved,distinctIdentities,friendPersisted;
            public string utc,scope,lastStage,safeError;public string[] checks,errors;public float seconds;
            public int[] cachedRelationshipsBeforeCleanupRefresh;public int retainedServiceRefreshes;
        }
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<GameObject> objects=new List<GameObject>();
        readonly List<IUnityServices> cloudInstances=new List<IUnityServices>();
        RunnerGame game;RunnerFriends a,b;RunnerMultiplayer peerA,peerB;bool auto,background,enabledExisted;int enabledPreference,wallet;float began;ulong compatibility;
        ServicesInitializationState defaultState;string defaultProfile;Report report=new Report();
        [MenuItem("Fener Runner/111 - Verify actual Friends and Relay invitations v20")]
        public static void StartChecks(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);new GameObject("Actual Friends v20 integration checks").AddComponent<FriendsCloudChecksV20>();}}
        IEnumerator Start()
        {
            began=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            game=FindFirstObjectByType<RunnerGame>();if(game!=null){auto=game.AutomatedTest;game.AutomatedTest=true;wallet=game.WalletBalance;game.MultiplayerInterface.Friends.DisableAutoConnectForTest();}
            enabledExisted=PlayerPrefs.HasKey(RunnerFriends.EnabledKey);enabledPreference=PlayerPrefs.GetInt(RunnerFriends.EnabledKey);
            defaultState=UnityServices.State;defaultProfile=defaultState==ServicesInitializationState.Initialized?AuthenticationService.Instance.Profile:"";
            compatibility=GhostProtocol.ComputeCompatibility("v20-isolated-friends-relay-invite-test");Application.logMessageReceived+=Log;
            var stack=new Stack<IEnumerator>();stack.Push(Scenarios());
            while(stack.Count>0)
            {
                bool more;object wait=null;try{more=stack.Peek().MoveNext();if(more)wait=stack.Peek().Current;}catch(Exception error){errors.Add("Integration stage "+report.lastStage+" failed: "+error.GetType().Name);break;}
                if(!more){stack.Pop();continue;}if(wait is IEnumerator nested){stack.Push(nested);continue;}yield return wait;
            }
            // Test relationships are scoped to two named test profiles only.
            if(a!=null&&b!=null&&a.Ready&&b.Ready&&a.Find(b.PlayerId)!=null)yield return Await(a.RemoveAsync(b.PlayerId),30);
            // A retained earlier same-player service can keep a stale cache after
            // a replacement instance is initialized; it is not the active UI peer.
            // First observe deletion on both active players, then reconcile only
            // retained service caches using the SDK's explicit refresh API.
            yield return Until(ActivePairDrained,12);
            Check(ActivePairDrained(),"Both active player subscriptions observe test-pair deletion before teardown");
            report.cachedRelationshipsBeforeCleanupRefresh=new int[cloudInstances.Count];
            for(int i=0;i<cloudInstances.Count;i++)
            {
                var instance=cloudInstances[i];report.cachedRelationshipsBeforeCleanupRefresh[i]=instance.GetFriendsService().Relationships.Count;
                if(instance==(a==null?null:a.ServicesForTest)||instance==(b==null?null:b.ServicesForTest))continue;
                report.retainedServiceRefreshes++;
                yield return CleanupAwait(instance.GetFriendsService().ForceRelationshipsRefreshAsync(),30);
            }
            yield return Wait(.5f);
            // SDK1.3 marks itself uninitialized before wire unsubscribe finishes.
            // No test relationship may remain in any SDK instance at sign-out.
            bool drained=TestRelationshipsDrained();Check(drained,"Every isolated service cache confirms pair deletion before authentication teardown, including refreshed retained instances");
            foreach(var obj in objects)
            {
                if(obj==null)continue;var social=obj.GetComponent<RunnerFriends>();var peer=obj.GetComponent<RunnerMultiplayer>();peer?.Leave();
                Destroy(obj);
            }
            if(drained)foreach(var instance in cloudInstances)instance.GetAuthenticationService().SignOut();
            yield return Wait(.75f);
            report.preferencesPreserved=(game==null||game.WalletBalance==wallet)&&enabledExisted==PlayerPrefs.HasKey(RunnerFriends.EnabledKey)&&enabledPreference==PlayerPrefs.GetInt(RunnerFriends.EnabledKey);
            report.defaultServicesPreserved=UnityServices.State==defaultState&&(defaultState!=ServicesInitializationState.Initialized||AuthenticationService.Instance.Profile==defaultProfile);
            Check(report.preferencesPreserved,"Cloud test preserves production wallet and social opt-in");Check(report.defaultServicesPreserved,"Isolated test services preserve production service identity and profile");
            Application.runInBackground=background;if(game!=null)game.AutomatedTest=auto;
            report.passed=errors.Count==0;report.actualCloud=true;report.utc=DateTime.UtcNow.ToString("O");report.checks=checks.ToArray();report.errors=errors.ToArray();report.seconds=Time.realtimeSinceStartup-began;
            report.scope="Real Unity Friends requests, acceptance, persisted relationship reload, direct structured messages and Relay invite acceptance between two isolated test auth profiles in one native Editor process. No production contacts/messages, no APK and no physical-phone claim.";
            Directory.CreateDirectory("../Validation");File.WriteAllText("../Validation/unity_friends_cloud_v20.json",JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;Debug.Log("Actual Friends v20 "+(report.passed?"PASSED":"FAILED")+" "+string.Join(";",errors));EditorApplication.isPlaying=false;
        }
        IEnumerator Scenarios()
        {
            report.lastStage="connect-isolated-profiles";a=Create(ProfileA,out peerA);b=Create(ProfileB,out peerB);
            yield return Await(Task.WhenAll(a.ConnectAsync(),b.ConnectAsync()),65);
            Remember(a);Remember(b);
            report.safeError=a.LastErrorCode+" / "+b.LastErrorCode;
            Check(a.Ready&&b.Ready,"Both isolated profiles initialize the real Friends backend");
            if(!a.Ready||!b.Ready)yield break;
            report.distinctIdentities=!string.IsNullOrEmpty(a.PlayerId)&&a.PlayerId!=b.PlayerId;Check(report.distinctIdentities,"Profiles have distinct real cloud player identities");
            Check(!string.IsNullOrEmpty(a.PlayerName)&&!string.IsNullOrEmpty(b.PlayerName),"Cloud player names are available for adding friends");
            yield return Until(()=>a.NotificationsConnected&&b.NotificationsConnected,20);
            Check(a.NotificationsConnected&&b.NotificationsConnected,"Both real Friends notification channels are subscribed");
            if(a.Find(b.PlayerId)!=null)yield return Await(a.RemoveAsync(b.PlayerId),30);
            yield return Await(Task.WhenAll(a.RefreshAsync(),b.RefreshAsync()),30);
            if(b.Find(a.PlayerId)!=null)yield return Await(b.RemoveAsync(a.PlayerId),30);
            yield return Await(Task.WhenAll(a.RefreshAsync(),b.RefreshAsync()),30);
            Check(a.Find(b.PlayerId)==null&&b.Find(a.PlayerId)==null,"Only the isolated test pair starts without a relationship");
            report.lastStage="friend-request-and-accept";
            yield return Await(a.AddByNameAsync(b.PlayerName),30);
            yield return Await(Task.WhenAll(a.RefreshAsync(),b.RefreshAsync()),30);
            Check(a.Find(b.PlayerId)?.Kind==RunnerFriendKind.Outgoing&&b.Find(a.PlayerId)?.Kind==RunnerFriendKind.Incoming,"Adding a full cloud player name produces outgoing and incoming requests");
            yield return Await(b.AcceptFriendAsync(a.PlayerId),30);
            yield return Await(Task.WhenAll(a.RefreshAsync(),b.RefreshAsync()),30);
            Check(a.Find(b.PlayerId)?.Kind==RunnerFriendKind.Friend&&b.Find(a.PlayerId)?.Kind==RunnerFriendKind.Friend,"Explicit acceptance creates reciprocal cloud friendship");
            report.lastStage="persisted-friend-reload";string originalId=a.PlayerId;
            // Keep the older scoped service authenticated until all pending
            // friend events drain at cleanup. The new instance still independently
            // restores the saved profile token and relationship from the backend.
            peerA.Leave();Destroy(a.gameObject);yield return Wait(.5f);
            a=Create(ProfileA,out peerA);yield return Await(a.ConnectAsync(),45);
            Remember(a);
            report.friendPersisted=a.Ready&&a.PlayerId==originalId&&a.Find(b.PlayerId)?.Kind==RunnerFriendKind.Friend;
            Check(report.friendPersisted,"New service instance restores saved identity and backend friendship without local friend-list storage");
            if(!a.Ready)yield break;
            yield return Await(Task.WhenAll(a.RefreshAsync(),b.RefreshAsync()),30);yield return Until(()=>a.NotificationsConnected&&b.NotificationsConnected,20);
            yield return Until(()=>a.Find(b.PlayerId)?.Online==true&&b.Find(a.PlayerId)?.Online==true,8);
            if(a.Find(b.PlayerId)?.Online!=true||b.Find(a.PlayerId)?.Online!=true)
                yield return Await(Task.WhenAll(a.RefreshAsync(),b.RefreshAsync()),30);
            Check(a.Find(b.PlayerId)?.Online==true&&b.Find(a.PlayerId)?.Online==true&&a.NotificationsConnected&&b.NotificationsConnected,
                "Real reciprocal presence and live notifications satisfy the actual DAVET ET button enablement rules");
            report.lastStage="direct-invite-delivery";
            yield return Await(a.InviteAsync(b.PlayerId),60);
            yield return Until(()=>b.Invites.Count>0,25);
            Check(peerA.IsHost&&peerA.State==MultiplayerState.Waiting&&!string.IsNullOrEmpty(peerA.JoinCode),"Invite action creates a real Relay room for the sender");
            Check(b.Invites.Count==1&&b.Invites[0].Code==peerA.JoinCode,"Structured room invitation arrives through real Friends messaging");
            Check(peerB.State==MultiplayerState.Offline&&!peerB.HasPeer,"Receiving cloud invite does not auto-join the recipient");
            if(b.Invites.Count==0)yield break;
            string invitation=b.Invites[0].Id;
            report.lastStage="explicit-invite-accept";var accepted=b.AcceptInviteAsync(invitation);yield return Await(accepted,60);
            Check(accepted.IsCompletedSuccessfully&&accepted.Result,"Recipient explicitly accepts the cloud invitation");
            yield return Until(()=>peerA.HasPeer&&peerB.HasPeer,20);
            Check(peerA.HasPeer&&peerB.HasPeer&&peerA.JoinCode==peerB.JoinCode,"Accepted invitation connects both actual Relay peers");
            Check(!peerA.LocalReady&&!peerB.LocalReady&&peerA.State==MultiplayerState.Waiting&&peerB.State==MultiplayerState.Waiting,"Invitation never bypasses either player's ready consent");
            Check(b.Invites.Count==0,"Accepted invitation leaves the inbox");
            report.lastStage="message-replay-and-expiry";
            var expired=new RunnerInvitePayload{kind="fener-runner-invite",version=20,id=Guid.NewGuid().ToString("N"),code=peerA.JoinCode,expires=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-1};
            yield return Await(a.ServiceForTest.MessageAsync(b.PlayerId,expired),30);yield return Wait(1);
            Check(b.Invites.Count==0,"Expired real message is ignored without interrupting the existing room");
            peerA.Leave();peerB.Leave();report.lastStage="complete";
        }
        RunnerFriends Create(string profile,out RunnerMultiplayer peer)
        {var obj=new GameObject("Isolated "+profile);objects.Add(obj);peer=obj.AddComponent<RunnerMultiplayer>();peer.InitializeTestPeer(compatibility,profile);var social=obj.AddComponent<RunnerFriends>();social.Initialize(null,peer);return social;}
        void Remember(RunnerFriends social){if(social.Ready&&social.ServicesForTest!=null&&!cloudInstances.Contains(social.ServicesForTest))cloudInstances.Add(social.ServicesForTest);}
        bool ActivePairDrained()=>a!=null&&b!=null&&a.Ready&&b.Ready&&a.Find(b.PlayerId)==null&&b.Find(a.PlayerId)==null;
        bool TestRelationshipsDrained()
        {
            foreach(var instance in cloudInstances)
                foreach(var relation in instance.GetFriendsService().Relationships)
                    if(relation.Member!=null&&((a!=null&&relation.Member.Id==a.PlayerId)||(b!=null&&relation.Member.Id==b.PlayerId)))return false;
            return true;
        }
        IEnumerator Await(Task task,float seconds)
        {float end=Time.realtimeSinceStartup+seconds;while(!task.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;if(!task.IsCompleted)throw new TimeoutException();if(task.IsFaulted)throw task.Exception;}
        IEnumerator CleanupAwait(Task task,float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;while(!task.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;
            if(!task.IsCompleted)errors.Add("Retained Friends cache refresh timed out during cleanup");
            else if(task.IsFaulted)errors.Add("Retained Friends cache refresh failed during cleanup: "+task.Exception.GetBaseException().GetType().Name);
            else if(task.IsCanceled)errors.Add("Retained Friends cache refresh cancelled during cleanup");
        }
        static IEnumerator Until(Func<bool> condition,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;}
        static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
    }
}
#endif
