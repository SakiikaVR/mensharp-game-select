using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CookieClickerGame : MenSharpBehaviour
{
    public Text balanceLabel, rateLabel, totalLabel, feedbackLabel;
    public Text[] shopLabels;
    public Button[] shopButtons;
    public RectTransform cookie;
    public AudioSource sound;
    public AudioClip clickSound, purchaseSound;
    public UdonBehaviour sessionController;
    public string[] upgradeNames;
    public float[] baseCosts, clickGains, productionGains;
    public float costMultiplier = 1.18f;
    public float initialClickPower = 1f;
    public float balance = 0f, totalBaked = 0f;
    public float clickPower = 0f, production = 0f;
    public int[] owned;
    public float[] costs;
    private bool initialized;
    private float pulse = 0f, refreshTime = 0f;

    public void Start() { StartGame(); }

    public void StartGame()
    {
        if (!initialized)
        {
            initialized = true;
            clickPower = initialClickPower;
            owned = new int[baseCosts.Length];
            costs = new float[baseCosts.Length];
            for (int i = 0; i < costs.Length; i++) costs[i] = baseCosts[i];
        }
        feedbackLabel.text = "クッキーをクリックして焼こう！";
        RefreshDisplay();
    }

    public void Bake()
    {
        if (!initialized) StartGame();
        balance += clickPower;
        totalBaked += clickPower;
        pulse = 1f;
        feedbackLabel.text = "+ " + Format(clickPower) + " cookies";
        if (sound != null && clickSound != null) sound.PlayOneShot(clickSound);
        RefreshDisplay();
    }
    public void BuyFirst() { Buy(0); }
    public void BuySecond() { Buy(1); }
    public void BuyThird() { Buy(2); }
    public void BuyFourth() { Buy(3); }
    private void Buy(int index)
    {
        if (!initialized || index >= costs.Length || balance < costs[index]) return;
        balance -= costs[index];
        owned[index]++;
        clickPower += clickGains[index];
        production += productionGains[index];
        costs[index] = Mathf.Ceil(costs[index] * costMultiplier);
        feedbackLabel.text = upgradeNames[index] + " を購入しました";
        if (sound != null && purchaseSound != null) sound.PlayOneShot(purchaseSound);
        RefreshDisplay();
    }
    public void Update()
    {
        if (!initialized) return;
        float amount = production * Time.deltaTime;
        balance += amount;
        totalBaked += amount;
        pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 6);
        cookie.localScale = Vector3.one * (1 + pulse * 0.075f);
        refreshTime += Time.deltaTime;
        if (refreshTime >= 0.1f) { refreshTime = 0f; RefreshDisplay(); }
    }
    public void BackToMenu()
    {
        if (sessionController != null)
            sessionController.SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestClose");
        else gameObject.SetActive(false);
    }
    private string Format(float value)
    {
        if (value >= 1000000000f) return (value / 1000000000f).ToString("F1") + " B";
        if (value >= 1000000f) return (value / 1000000f).ToString("F1") + " M";
        if (value >= 1000f) return (value / 1000f).ToString("F1") + " K";
        return Mathf.Floor(value).ToString("F0");
    }
    public void RefreshDisplay()
    {
        balanceLabel.text = Format(balance);
        rateLabel.text = Format(clickPower) + " / CLICK     ·     " + Format(production) + " / SEC";
        totalLabel.text = "TOTAL BAKED  " + Format(totalBaked);
        for (int i = 0; i < costs.Length; i++)
        {
            shopLabels[i].text = upgradeNames[i] + "   ×" + owned[i].ToString()
                + "\n" + (clickGains[i] > 0 ? "+" + Format(clickGains[i]) + " / CLICK" : "+" + Format(productionGains[i]) + " / SEC")
                + "     COST  " + Format(costs[i]);
            shopButtons[i].interactable = balance >= costs[i];
        }
    }
}

