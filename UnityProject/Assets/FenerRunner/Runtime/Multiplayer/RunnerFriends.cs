using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;
using Unity.Services.Friends.Exceptions;

namespace FenerRunner
{
    public enum RunnerFriendKind { Friend, Incoming, Outgoing }
    public sealed class RunnerFriend
    {
        public string Id,Name;public RunnerFriendKind Kind;public bool Online;
    }
    [Serializable,UnityEngine.Scripting.Preserve] public sealed class RunnerInvitePayload
    {
        [UnityEngine.Scripting.Preserve] public string kind,id,code;
        [UnityEngine.Scripting.Preserve] public int version;
        [UnityEngine.Scripting.Preserve] public long expires;
        [UnityEngine.Scripting.Preserve] public RunnerInvitePayload(){}
    }
    public sealed class RunnerFriendInvite
    {
        public string Id,SenderId,SenderName,Code;public long Expires;
    }

    // The Friends backend owns relationships and the saved Authentication token
    // owns identity. Only explicitly accepted invites may change the Relay room.
    public sealed class RunnerFriends : MonoBehaviour
    {
        public const string EnabledKey="FenerRunner.Social.Enabled";
        public const int InviteLifetime=120,MaxInvites=5;
        readonly List<RunnerFriend> rows=new List<RunnerFriend>();
        readonly List<RunnerFriendInvite> invites=new List<RunnerFriendInvite>();
        readonly HashSet<string> seen=new HashSet<string>();readonly Queue<string> seenOrder=new Queue<string>();
        readonly Dictionary<string,double> sentAt=new Dictionary<string,double>();
        RunnerGame game;RunnerMultiplayer session;IUnityServices services;IFriendsService friends;
        Task connectTask,friendsTask;bool subscribed,destroyed,operation,refreshing,presencePending;int generation,roomGeneration;
        float expiryClock;double nextPresence;Availability shownAvailability=Availability.Unknown;
        public event Action Changed;
        public event Action InviteArrived;
        public bool Ready {get;private set;}
        public bool Connecting {get;private set;}
        public bool Busy=>Connecting||operation;
        public bool NotificationsConnected {get;private set;}
        public string PlayerName {get;private set;}="";
        public string PlayerId {get;private set;}="";
        public string Status {get;private set;}="Arkadaşını ekle, oyun içinden davet et.";
        public string LastErrorCode {get;private set;}="";
        public IReadOnlyList<RunnerFriend> People=>rows;
        public IReadOnlyList<RunnerFriendInvite> Invites=>invites;
        static long Now=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public void Initialize(RunnerGame owner,RunnerMultiplayer multiplayer)
        {
            game=owner;session=multiplayer;if(game!=null)StartCoroutine(RestoreConnection());
        }
        IEnumerator RestoreConnection()
        {
            yield return null;yield return new WaitForSecondsRealtime(1);
#if UNITY_EDITOR
            if(game==null||game.AutomatedTest)yield break;
#endif
            if(PlayerPrefs.GetInt(EnabledKey,0)==1)_=ConnectAsync();
        }
        public Task ConnectAsync()
        {
            if(destroyed)return Task.CompletedTask;
            if(connectTask!=null&&!connectTask.IsCompleted)return connectTask;
            // Wire owns reconnect. Signing out here would invalidate Friends
            // while already queued wire notifications are still being delivered.
            if(Ready)return RefreshAsync();
            connectTask=ConnectCore();return connectTask;
        }
        async Task ConnectCore()
        {
            Connecting=true;Status="Arkadaş listene bağlanılıyor…";LastErrorCode="";Notify();
            int request=++generation;
            try
            {
                services=await session.GetAuthenticatedServicesAsync();if(!Current(request))return;
                var auth=services.GetAuthenticationService();PlayerId=auth.PlayerId;
                PlayerName=await Bounded(auth.GetPlayerNameAsync());if(!Current(request))return;
                friends=services.GetFriendsService();Subscribe();
                if(friendsTask==null||friendsTask.IsFaulted||friendsTask.IsCanceled)
                    friendsTask=friends.InitializeAsync(new InitializeOptions().WithMemberPresence(true).WithMemberProfile(true).WithEvents(true));
                await Bounded(friendsTask);
                if(!Current(request))return;
                Ready=true;Rebuild();Status="Arkadaşlarını ekle. Davetler oyun açıkken gelir.";
                if(game!=null)
                {
#if UNITY_EDITOR
                    if(!game.AutomatedTest)
#endif
                    {PlayerPrefs.SetInt(EnabledKey,1);PlayerPrefs.Save();}
                }
                await PublishPresence();
            }
            catch(Exception error)
            {if(Current(request)){Ready=false;Status=ErrorText(error);LastErrorCode=SafeCode(error);}}
            finally{if(Current(request)){Connecting=false;Notify();}}
        }
        bool Current(int request)=>!destroyed&&generation==request;
        void Subscribe()
        {
            if(subscribed)return;subscribed=true;
            friends.RelationshipAdded+=Added;friends.RelationshipDeleted+=Deleted;friends.PresenceUpdated+=Presence;
            friends.MessageReceived+=Message;friends.NotificationsConnectivityChanged+=Connectivity;
        }
        void Unsubscribe()
        {
            if(!subscribed||friends==null)return;subscribed=false;
            friends.RelationshipAdded-=Added;friends.RelationshipDeleted-=Deleted;friends.PresenceUpdated-=Presence;
            friends.MessageReceived-=Message;friends.NotificationsConnectivityChanged-=Connectivity;
            NotificationsConnected=false;
        }
        void Added(IRelationshipAddedEvent value){Rebuild();}
        void Deleted(IRelationshipDeletedEvent value){Rebuild();PruneInvites();}
        void Presence(IPresenceUpdatedEvent value){Rebuild();}
        void Connectivity(INotificationsStateChangedEvent value)
        {
            NotificationsConnected=value.State==FriendsEventConnectionState.Subscribed;
            if(Ready&&!NotificationsConnected)Status="Canlı davet bağlantısı bekleniyor. Bağlantı gelince tekrar dene.";
            else if(Ready)Status="Arkadaş listesi güncel. Oyun içi davetlere hazırsın.";
            Notify();
        }
        void Rebuild()
        {
            if(!Ready||friends==null)return;rows.Clear();
            Append(friends.IncomingFriendRequests,RunnerFriendKind.Incoming);
            Append(friends.Friends,RunnerFriendKind.Friend);Append(friends.OutgoingFriendRequests,RunnerFriendKind.Outgoing);
            rows.Sort((a,b)=>{int rank=Rank(a).CompareTo(Rank(b));return rank==0?string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase):rank;});Notify();
        }
        static int Rank(RunnerFriend row)=>row.Kind==RunnerFriendKind.Incoming?0:row.Kind==RunnerFriendKind.Outgoing?3:row.Online?1:2;
        void Append(IReadOnlyList<Relationship> source,RunnerFriendKind kind)
        {
            foreach(var item in source)
            {
                var member=item.Member;if(member==null||string.IsNullOrEmpty(member.Id))continue;
                rows.Add(new RunnerFriend{Id=member.Id,Name=CleanDisplay(member.Profile?.Name,32),Kind=kind,
                    Online=member.Presence!=null&&(member.Presence.Availability==Availability.Online||member.Presence.Availability==Availability.Busy||member.Presence.Availability==Availability.Away)});
            }
        }
        public async Task RefreshAsync()
        {
            if(!Ready||refreshing||destroyed)return;refreshing=true;
            try{await Bounded(friends.ForceRelationshipsRefreshAsync());if(!destroyed){Rebuild();Status=NotificationsConnected?"Arkadaş listesi güncellendi.":"Liste güncel. Canlı davet bağlantısı bekleniyor.";}}
            catch(Exception error){if(!destroyed){Status=ErrorText(error);LastErrorCode=SafeCode(error);}}
            finally{refreshing=false;Notify();}
        }
        public Task AddByNameAsync(string name)
        {
            name=(name??"").Trim();
            if(!ValidFriendName(name)||string.Equals(name,PlayerName,StringComparison.OrdinalIgnoreCase))
            {Status="Arkadaşının tam adını #etiketiyle yaz. Kendini ekleyemezsin.";Notify();return Task.CompletedTask;}
            return Perform(()=>friends.AddFriendByNameAsync(name),"Arkadaşlık isteği gönderildi.");
        }
        public Task RenameAsync(string name)
        {
            name=(name??"").Trim();
            if(name.Length<3||name.Length>18){Status="Adın 3–18 karakter olmalı.";Notify();return Task.CompletedTask;}
            foreach(char c in name)if(!char.IsLetterOrDigit(c)&&c!='_'){Status="Adında yalnızca harf, rakam ve _ kullan.";Notify();return Task.CompletedTask;}
            return Perform(async()=>{PlayerName=await services.GetAuthenticationService().UpdatePlayerNameAsync(name);},"Oyuncu adın güncellendi.");
        }
        public Task AcceptFriendAsync(string id)=>IsKind(id,RunnerFriendKind.Incoming)?Perform(()=>friends.AddFriendAsync(id),"Arkadaş eklendi. Artık birbirinizi davet edebilirsiniz."):Task.CompletedTask;
        public Task RemoveAsync(string id)
        {
            var row=Find(id);if(row==null)return Task.CompletedTask;
            return Perform(()=>row.Kind==RunnerFriendKind.Incoming?friends.DeleteIncomingFriendRequestAsync(id):row.Kind==RunnerFriendKind.Outgoing?friends.DeleteOutgoingFriendRequestAsync(id):friends.DeleteFriendAsync(id),"Liste güncellendi.");
        }
        async Task Perform(Func<Task> action,string success)
        {
            if(!Ready||Busy||destroyed)return;operation=true;Notify();
            try{await Bounded(action());if(!destroyed){Status=success;Rebuild();}}
            catch(Exception error){if(!destroyed){Status=ErrorText(error);LastErrorCode=SafeCode(error);}}
            finally{operation=false;Notify();}
        }
        public async Task InviteAsync(string friendId)
        {
            if(!Ready||Busy||destroyed||!IsKind(friendId,RunnerFriendKind.Friend))return;
            if(!NotificationsConnected){Status="Davet bağlantısı hazır değil. Yenile ve tekrar dene.";Notify();return;}
            if(game!=null&&game.State!=RunState.Menu&&game.State!=RunState.Finished){Status="Koşun bitince arkadaşını davet edebilirsin.";Notify();return;}
            double clock=Time.realtimeSinceStartupAsDouble;
            if(sentAt.TryGetValue(friendId,out var last)&&clock-last<12){Status="Davet zaten gönderildi. Biraz bekle.";Notify();return;}
            operation=true;int request=generation,roomRequest=roomGeneration;Notify();
            try
            {
                if(!session.IsActive)await session.HostAsync();
                if(!Current(request)||roomRequest!=roomGeneration)return;
                if(!session.IsHost||session.State!=MultiplayerState.Waiting||session.HasPeer||!RunnerMultiplayer.ValidCode(session.JoinCode))
                {Status="Davet için boş bir oda oluşturmalısın.";return;}
                var payload=new RunnerInvitePayload{kind="fener-runner-invite",version=20,id=Guid.NewGuid().ToString("N"),code=session.JoinCode,expires=Now+InviteLifetime};
                await Bounded(friends.MessageAsync(friendId,payload));
                if(!Current(request)||roomRequest!=roomGeneration)return;sentAt[friendId]=Time.realtimeSinceStartupAsDouble;
                Status="Davet gönderildi. Arkadaşın oyun içinden katılabilir.";
            }
            catch(Exception error){if(Current(request)&&roomRequest==roomGeneration){Status=ErrorText(error);LastErrorCode=SafeCode(error);}}
            finally{operation=false;Notify();}
        }
        void Message(IMessageReceivedEvent incoming)
        {
            if(!Ready||destroyed)return;
            try{Receive(incoming.UserId,incoming.GetAs<RunnerInvitePayload>(),Now);}catch{ /* malformed foreign messages are discarded */ }
        }
        bool Receive(string sender,RunnerInvitePayload payload,long now)
        {
            var person=Find(sender);
            if(person==null||person.Kind!=RunnerFriendKind.Friend||!ValidInvite(payload,now)||!seen.Add(payload.id))return false;
            seenOrder.Enqueue(payload.id);while(seenOrder.Count>64)seen.Remove(seenOrder.Dequeue());
            // One current invite per friend; the small inbox stays bounded.
            invites.RemoveAll(x=>x.SenderId==sender||x.Expires<=now);
            if(invites.Count>=MaxInvites)invites.RemoveAt(0);
            invites.Add(new RunnerFriendInvite{Id=payload.id,SenderId=sender,SenderName=person.Name,Code=payload.code,Expires=payload.expires});
            Notify();InviteArrived?.Invoke();return true;
        }
        public async Task<bool> AcceptInviteAsync(string id)
        {
            if(!Ready||Busy||destroyed)return false;PruneInvites();
            var invite=invites.Find(x=>x.Id==id);if(invite==null)return false;
            if(game!=null&&game.State!=RunState.Menu&&game.State!=RunState.Finished){Status="Önce koşunu bitir veya ana menüye dön.";Notify();return false;}
            if(session.IsActive){Status="Önce mevcut odadan ayrıl, sonra daveti kabul et.";Notify();return false;}
            operation=true;int roomRequest=roomGeneration;Notify();bool joined=false;
            try
            {
                // This is the only receive path that joins. Receiving a message
                // never changes gameplay, balance, current room or readiness.
                await session.JoinAsync(invite.Code);
                double deadline=Time.realtimeSinceStartupAsDouble+24;
                while(!destroyed&&roomRequest==roomGeneration&&session.State==MultiplayerState.Connecting&&Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(25);
                if(destroyed||roomRequest!=roomGeneration)return false;
                joined=session.State==MultiplayerState.Waiting&&session.HasPeer;
                if(joined){invites.Remove(invite);Status="Arkadaşının odasına katıldın.";}
                else Status="Davetin odası kapanmış olabilir. Arkadaşından yeni davet iste.";
                return joined;
            }
            finally{operation=false;Notify();}
        }
        public void DismissInvite(string id){invites.RemoveAll(x=>x.Id==id);Notify();}
        public void CancelRoomActions(){roomGeneration++;}
        void PruneInvites()
        {int count=invites.RemoveAll(x=>x.Expires<=Now||!IsKind(x.SenderId,RunnerFriendKind.Friend));if(count>0)Notify();}
        public RunnerFriend Find(string id)=>rows.Find(x=>x.Id==id);
        bool IsKind(string id,RunnerFriendKind kind){var value=Find(id);return value!=null&&value.Kind==kind;}
        public static bool ValidFriendName(string value)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Length>48||value.Length<3)return false;
            int hash=value.LastIndexOf('#');if(hash<1||hash>=value.Length-1)return false;
            foreach(char c in value)if(char.IsControl(c)||c=='<'||c=='>')return false;return true;
        }
        public static bool ValidInvite(RunnerInvitePayload value,long now)
        {
            if(value==null||value.kind!="fener-runner-invite"||value.version!=20||!RunnerMultiplayer.ValidCode(value.code)||value.expires<=now||value.expires>now+InviteLifetime+30||value.id==null||value.id.Length!=32)return false;
            foreach(char c in value.id)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;return true;
        }
        public static string CleanDisplay(string value,int cap)
        {
            if(string.IsNullOrWhiteSpace(value))return "Taraftar";
            var clean=new System.Text.StringBuilder();foreach(char c in value){if(!char.IsControl(c))clean.Append(c);if(clean.Length>=cap)break;}return clean.Length==0?"Taraftar":clean.ToString();
        }
        void Update()
        {
            if(!Ready)return;expiryClock-=Time.unscaledDeltaTime;if(expiryClock<=0){expiryClock=1;PruneInvites();}
            if(Time.realtimeSinceStartupAsDouble>=nextPresence){nextPresence=Time.realtimeSinceStartupAsDouble+8;_=PublishPresence();}
        }
        async Task PublishPresence()
        {
            if(!Ready||destroyed||presencePending)return;
            var next=game!=null&&game.State==RunState.Running?Availability.Busy:Availability.Online;
            if(next==shownAvailability)return;
            presencePending=true;
            try{await Bounded(friends.SetPresenceAvailabilityAsync(next));if(!destroyed)shownAvailability=next;}catch{ /* next update retries without interrupting the run */ }
            finally{presencePending=false;}
        }
        void OnApplicationPause(bool paused)
        {if(!paused&&Ready){nextPresence=0;_=RefreshAsync();}}
        void Notify(){if(!destroyed)Changed?.Invoke();}
        void OnDestroy(){destroyed=true;++generation;Unsubscribe();}
        public static string ErrorText(Exception error)
        {
            if(error is FriendsServiceException service)
            {
                if(service.StatusCode==HttpStatusCode.Forbidden)return "Arkadaş hizmeti henüz kullanılamıyor. Sonra yeniden dene.";
                if(service.StatusCode==HttpStatusCode.NotFound)return "Oyuncu bulunamadı. Adını #etiketiyle birlikte kontrol et.";
                if((int)service.StatusCode==429)return "Biraz hızlı işlem yaptın. Birkaç saniye bekleyip yeniden dene.";
            }
            return "Arkadaş bağlantısı kurulamadı. İnternetini kontrol edip yeniden dene.";
        }
        static string SafeCode(Exception error)=>error is FriendsServiceException service?"Friends:"+(int)service.StatusCode+":"+service.ErrorCode:error.GetType().Name;
        static async Task Bounded(Task task)
        {if(await Task.WhenAny(task,Task.Delay(20000))!=task){Observe(task);throw new TimeoutException();}await task;}
        static async Task<T> Bounded<T>(Task<T> task){await Bounded((Task)task);return await task;}
        static async void Observe(Task task){try{await task;}catch{}}
#if UNITY_EDITOR
        public IFriendsService ServiceForTest=>friends;
        public IUnityServices ServicesForTest=>services;
        public void DisableAutoConnectForTest(){StopAllCoroutines();}
        public void SeedFriendForTest(string id,string name,RunnerFriendKind kind=RunnerFriendKind.Friend)
        {if(Ready||Connecting)throw new InvalidOperationException("Offline fixture only");rows.Add(new RunnerFriend{Id=id,Name=name,Kind=kind,Online=true});Notify();}
        public void ClearFixturesForTest()
        {if(Ready||Connecting)throw new InvalidOperationException("Offline fixture only");rows.Clear();invites.Clear();seen.Clear();seenOrder.Clear();Notify();}
        public bool ReceiveForTest(string sender,RunnerInvitePayload payload,long now)=>Receive(sender,payload,now);
#endif
    }
}
