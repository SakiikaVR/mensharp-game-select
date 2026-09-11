using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Display-only, per-player billboard. Package data is supplied through optional Text fields.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GameRoomStatusMonitor : MenSharpBehaviour
{
    public Transform screen;
    public GameObject[] sourceRoots;
    public Text[] sourcePiles, sourceStates, sourceTurns;
    public Text pileLabel, stateLabel, turnLabel;
    private float elapsed = 0f;

    public void LateUpdate()
    {
        if (Networking.LocalPlayer != null)
        {
            Vector3 head = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.Head);
            if (head == Vector3.zero) head = Networking.LocalPlayer.GetPosition() + Vector3.up * 1.6f;
            Vector3 away = screen.position - head;
            if (away.sqrMagnitude > .01f) screen.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
        elapsed += Time.deltaTime;
        if (elapsed < .1f) return;
        elapsed = 0f;
        RefreshDisplay();
    }
    public void RefreshDisplay()
    {
        if (sourceRoots != null)
        {
            for (int i = 0; i < sourceRoots.Length; i++)
            {
                if (sourceRoots[i] == null || !sourceRoots[i].activeInHierarchy || sourcePiles[i] == null) continue;
                pileLabel.text = sourcePiles[i].text;
                stateLabel.text = sourceStates[i].text;
                turnLabel.text = sourceTurns[i].text;
                return;
            }
        }
        pileLabel.text = "GAME TABLE";
        stateLabel.text = "対戦が始まると\n場のカードと状態を表示します";
        turnLabel.text = "外側のモニターからゲームを選択";
    }
}
