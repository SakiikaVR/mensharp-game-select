using MenSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameRoomSession : MenSharpBehaviour
{
    public UdonBehaviour[] monitors;
    public UdonBehaviour self;
    public string[] installedGameIds;
    [UdonSynced] public string activeGameId = "";

    public void Start() { if (self != null) self.SendCustomEventDelayedFrames("_ApplyState", 2); }
    public void OnDeserialization() { _ApplyState(); }
    public void OnOwnershipTransferred(VRCPlayerApi player) { _ApplyState(); }
    public void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) RequestSerialization();
    }

    [NetworkCallable(maxEventsPerSecond: 4)]
    public void RequestOpen(string gameId)
    {
        // Requests go to the current owner; the owner is the single writer.
        if (!Networking.IsOwner(gameObject) || activeGameId != "") return;
        bool installed = false;
        for (int i = 0; i < installedGameIds.Length; i++)
            if (installedGameIds[i] == gameId) installed = true;
        if (!installed) return;
        activeGameId = gameId;
        RequestSerialization();
        _ApplyState();
    }

    [NetworkCallable(maxEventsPerSecond: 4)]
    public void RequestClose()
    {
        if (!Networking.IsOwner(gameObject)) return;
        activeGameId = "";
        RequestSerialization();
        _ApplyState();
    }

    public void _ApplyState()
    {
        if (monitors == null) return;
        for (int i = 0; i < monitors.Length; i++)
        {
            if (monitors[i] == null) continue;
            monitors[i].SetProgramVariable("sessionGameId", (object)activeGameId);
            monitors[i].SendCustomEvent("_ApplySession");
        }
    }
}
