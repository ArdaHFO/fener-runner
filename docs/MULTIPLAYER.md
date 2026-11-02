# Friends and ghost races

[English](MULTIPLAYER.md) · [Türkçe](MULTIPLAYER.tr.md) · [Back to the project](../README.md)

## How it works

Two players run the same seeded course while seeing each other as translucent ghosts. Each device owns its local movement, collisions, collectibles and wallet. Remote snapshots animate the ghost and do not award currency or control the local player.

The game uses Unity Gaming Services for anonymous authentication, Friends and Relay. Players do not need to host their own server or configure router port forwarding. Internet access is required for online play, and service availability applies.

This is a two-player ghost race, not a shared collision simulation. Both players must use a compatible game version. There is no background push notification service.

## Add a friend and invite them

Keep both games open and online.

1. Open **ARKADAŞINLA KOŞ → ARKADAŞLARIM · DAVETLER** from the menu. If prompted, choose **BAĞLAN**.
2. Copy your full player name from the top of the screen. It includes a tag, in the form `name#tag`.
3. Enter your friend's complete name and choose **EKLE**.
4. The recipient opens **İSTEKLER** and chooses **KABUL ET**.
5. Once your friend is online, choose **DAVET ET** beside their name. An empty room is created when needed.
6. The recipient opens **DAVETLER** and chooses **KATIL**.
7. Both players choose **HAZIRIM** in the room. The shared countdown starts the race.

An invitation lasts **two minutes**. Send another if it expires. There is a short invitation cooldown. Joining is always a recipient action, never an automatic response to an incoming invitation.

## Join with a room code

Use the room creation and room-code join controls under **ARKADAŞINLA KOŞ**. The host shares the displayed code with the second player. Once connected, both players choose **HAZIRIM**.

Leave your current room before accepting an invitation to another. Returning to the main menu from a multiplayer run also leaves that room.

## Identity and local progress

Authentication is anonymous and retained on the device. Accepted friendships are stored by the online Friends service. Local progress is stored separately on the device.

Uninstalling the app or clearing its data may remove access to the anonymous identity and its friend list. A player name is not an account-recovery credential. This preview does not provide a user-facing account-linking or recovery flow. See [privacy](PRIVACY.md).

## Developing online features

For your own deployment, link your development copy to a Unity Cloud project you control and configure **Authentication, Relay and Friends**. Restore the pinned packages before testing. Use the Unity Editor's project/service configuration rather than adding service account credentials to the client or repository.

The published APK retains the release's configured backend. A fork configured against another cloud project belongs to a different online environment. Do not assume that its identities, friends or room codes will interoperate with the downloadable release.

Live cloud verification creates test identities and performs real service operations. Run it deliberately against your development project. Historical setup checks may assert release-specific project identifiers or versions, so review those assumptions when adapting the harness to another backend.

For the transport and state flow, start with:

```text
UnityProject/Assets/FenerRunner/Runtime/Multiplayer/
```

`RunnerMultiplayer` coordinates authenticated Relay sessions and run state. `RunnerFriends` handles friendships and invitation messages. `GhostRunnerView` renders the remote runner. The protocol includes compatibility checks, bounded messages and remote snapshot handling.

## What has been verified

The v0.20 evidence includes real Friends request/acceptance, persistence, presence and invitations, plus real Relay connectivity using Editor test identities. Native scene gameplay was also tested with a controlled loopback peer.

The gallery's ghost image is a real Editor capture from that local-peer scenario. It does not establish two-physical-phone operation, mobile network reliability or Android performance. See the full [validation scope](VALIDATION.md).
