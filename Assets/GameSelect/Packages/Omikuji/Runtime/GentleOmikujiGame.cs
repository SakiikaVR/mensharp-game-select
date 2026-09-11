using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GentleOmikujiGame : MenSharpBehaviour
{
    public UdonBehaviour sessionController;
    public Text rankLabel, messageLabel, luckyLabel, countLabel, drawLabel;
    public Button drawButton;
    public RectTransform shrine;
    public AudioSource sound;
    public AudioClip revealSound;
    public string[] messages, luckyItems;
    public int resultRank = -1;
    public int drawCount = 0;
    public bool isDrawing;
    private float elapsed = 0f;
    private string lastMessage = "", lastLucky = "";

    public void Start() { StartGame(); }
    public void StartGame()
    {
        isDrawing = false;
        elapsed = 0f;
        shrine.localEulerAngles = Vector3.zero;
        drawButton.interactable = true;
        drawLabel.text = drawCount == 0 ? "おみくじを引く" : "もう一度引く";
        if (drawCount == 0)
        {
            rankLabel.text = "福";
            messageLabel.text = "あなたの運勢を占います";
            luckyLabel.text = "おみくじを引いてください";
        }
        else
        {
            rankLabel.text = resultRank == 0 ? "中吉" : "大吉";
            messageLabel.text = lastMessage;
            luckyLabel.text = lastLucky;
        }
        countLabel.text = "抽選回数 " + drawCount.ToString() + " 回";
    }
    public void DrawFortune()
    {
        if (isDrawing) return;
        isDrawing = true; elapsed = 0f;
        drawButton.interactable = false;
        drawLabel.text = "抽選中…";
        rankLabel.text = "…";
        messageLabel.text = "しばらくお待ちください";
        luckyLabel.text = "";
    }
    public void Update()
    {
        if (!isDrawing) return;
        elapsed += Time.deltaTime;
        shrine.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(elapsed * 27f) * 5f);
        if (elapsed >= 0.85f) _RevealFortune();
    }
    public void _RevealFortune()
    {
        if (!isDrawing) return;
        isDrawing = false;
        shrine.localEulerAngles = Vector3.zero;
        // The only outcomes, intentionally fixed: no lower fortunes can be configured.
        resultRank = Random.Range(0, 2);
        rankLabel.text = resultRank == 0 ? "中吉" : "大吉";
        messageLabel.text = messages[Random.Range(0, messages.Length)];
        luckyLabel.text = "今日のラッキーアイテム\n" + luckyItems[Random.Range(0, luckyItems.Length)];
        lastMessage = messageLabel.text; lastLucky = luckyLabel.text;
        drawCount++;
        countLabel.text = "抽選回数 " + drawCount.ToString() + " 回";
        drawLabel.text = "もう一度引く";
        drawButton.interactable = true;
        if (sound != null && revealSound != null) sound.PlayOneShot(revealSound);
    }
    public void BackToMenu()
    {
        if (sessionController != null)
            sessionController.SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestClose");
        else gameObject.SetActive(false);
    }
}
