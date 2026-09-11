using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CookieClickerGame : MenSharpBehaviour
{
    public Text balanceLabel, rateLabel, totalLabel, feedbackLabel;
    public Text[] shopLabels;
    public Button[] shopButtons;
    public RectTransform cookie;
    public AudioSource sound;
    public AudioClip clickSound, purchaseSound;
    public UdonBehaviour sessionController;
    public UdonBehaviour self, table;
    public UdonBehaviour[] roomViews;
    public Text sharedPileLabel, sharedStateLabel, sharedTurnLabel;
    [UdonSynced] public int[] scoreIds = new int[80];
    [UdonSynced] public string[] scoreNames = new string[80];
    [UdonSynced] public float[] scores = new float[80];
    private float reportTime = 3f, boardTime = 0f;
    private float lastReported = -1f;
    private int boardPage = 0;
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
        if (table == null) table = self;
        if (table != self) { table.SendCustomEvent("StartGame"); RefreshDisplay(); return; }
        if (!initialized)
        {
            initialized = true;
            clickPower = initialClickPower;
            owned = new int[baseCosts.Length];
            costs = new float[baseCosts.Length];
            for (int i = 0; i < costs.Length; i++) costs[i] = baseCosts[i];
        }
        feedbackLabel.text = "タップして、おやつを焼こう！";
        lastReported=-1f;
        if(Networking.IsOwner(gameObject))
        {
            for(int i=0;i<80;i++)if(scoreIds[i]>0 && VRCPlayerApi.GetPlayerById(scoreIds[i])==null){scoreIds[i]=0;scoreNames[i]="";scores[i]=0f;}
            RequestSerialization();
        }
        DrawLeaderboard();
        RefreshDisplay();
    }

    public void Bake()
    {
        if (table != self) { table.SendCustomEvent("Bake"); return; }
        if (!initialized) StartGame();
        balance += clickPower;
        totalBaked += clickPower;
        pulse = 1f;
        feedbackLabel.text = "+ " + Format(clickPower) + " 個できました";
        if (sound != null && clickSound != null) sound.PlayOneShot(clickSound);
        RefreshDisplay();
    }
    public void BuyFirst() { if (table != self) table.SendCustomEvent("BuyFirst"); else Buy(0); }
    public void BuySecond() { if (table != self) table.SendCustomEvent("BuySecond"); else Buy(1); }
    public void BuyThird() { if (table != self) table.SendCustomEvent("BuyThird"); else Buy(2); }
    public void BuyFourth() { if (table != self) table.SendCustomEvent("BuyFourth"); else Buy(3); }
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
        if (table != self) return;
        if (!initialized) return;
        float amount = production * Time.deltaTime;
        balance += amount;
        totalBaked += amount;
        pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 6);
        cookie.localScale = Vector3.one * (1 + pulse * 0.075f);
        refreshTime += Time.deltaTime;
        if (refreshTime >= 0.1f)
        {
            refreshTime = 0f; RefreshDisplay();
            if (roomViews != null) for (int i=0;i<roomViews.Length;i++) if(roomViews[i]!=null && roomViews[i]!=self) roomViews[i].SendCustomEvent("RefreshDisplay");
        }
        reportTime += Time.deltaTime;
        if (reportTime >= 3f && Networking.LocalPlayer != null)
        {
            reportTime = 0f;
            if (lastReported != totalBaked) { self.SendCustomNetworkEvent(NetworkEventTarget.Owner,"ReportScore",totalBaked); lastReported=totalBaked; }
            DrawLeaderboard();
        }
        boardTime += Time.deltaTime;
        if (boardTime >= 8f) { boardTime=0f; boardPage++; DrawLeaderboard(); }
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
        if (table != null && table != self)
        {
            balance=(float)table.GetProgramVariable("balance");totalBaked=(float)table.GetProgramVariable("totalBaked");
            clickPower=(float)table.GetProgramVariable("clickPower");production=(float)table.GetProgramVariable("production");
            owned=(int[])table.GetProgramVariable("owned");costs=(float[])table.GetProgramVariable("costs");
        }
        if (costs == null) return;
        balanceLabel.text = Format(balance);
        rateLabel.text = Format(clickPower) + " / CLICK     ·     " + Format(production) + " / SEC";
        totalLabel.text = "これまでの焼き上がり  " + Format(totalBaked);
        for (int i = 0; i < costs.Length; i++)
        {
            shopLabels[i].text = upgradeNames[i] + "   ×" + owned[i].ToString()
                + "\n" + (clickGains[i] > 0 ? "+" + Format(clickGains[i]) + " / CLICK" : "+" + Format(productionGains[i]) + " / SEC")
                + "     COST  " + Format(costs[i]);
            shopButtons[i].interactable = balance >= costs[i];
        }
    }
    [NetworkCallable(maxEventsPerSecond: 50)]
    public void ReportScore(float value)
    {
        if (table != self || !Networking.IsOwner(gameObject)) return;
        var caller=NetworkCalling.CallingPlayer;
        if (caller==null || !(value>=0f && value<=1e30f)) return;
        int slot=-1;
        for(int i=0;i<80;i++)if(scoreIds[i]==caller.playerId){slot=i;break;}
        if(slot<0)for(int i=0;i<80;i++)if(scoreIds[i]==0){slot=i;break;}
        if(slot<0)return;
        scoreIds[slot]=caller.playerId;scoreNames[slot]=caller.displayName;scores[slot]=Mathf.Max(scores[slot],value);
        RequestSerialization();DrawLeaderboard();
    }
    public void OnDeserialization() { if(table==self)DrawLeaderboard(); }
    public void OnPlayerJoined(VRCPlayerApi player) { if(table==self && Networking.IsOwner(gameObject))RequestSerialization(); }
    public void OnPlayerLeft(VRCPlayerApi player)
    {
        if(table!=self || !Networking.IsOwner(gameObject))return;
        for(int i=0;i<80;i++)if(scoreIds[i]==player.playerId){scoreIds[i]=0;scoreNames[i]="";scores[i]=0f;}
        RequestSerialization();DrawLeaderboard();
    }
    public void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if(table!=self || !Networking.IsOwner(gameObject))return;
        for(int i=0;i<80;i++)if(scoreIds[i]>0 && VRCPlayerApi.GetPlayerById(scoreIds[i])==null){scoreIds[i]=0;scoreNames[i]="";scores[i]=0f;}
        RequestSerialization();DrawLeaderboard();
    }
    public void DrawLeaderboard()
    {
        if(sharedPileLabel==null)return;
        int[] order=new int[80];int count=0;
        for(int i=0;i<80;i++)if(scoreIds[i]>0)
        {
            int j=count;
            while(j>0 && scores[order[j-1]]<scores[i]){order[j]=order[j-1];j--;}
            order[j]=i;count++;
        }
        int pages=Mathf.Max(1,(count+5)/6);boardPage=boardPage%pages;
        sharedPileLabel.text="おやつ工房 / みんなのスコア";
        sharedStateLabel.text="";
        for(int row=boardPage*6;row<Mathf.Min(count,boardPage*6+6);row++)
        {
            int i=order[row];string n=scoreNames[i];if(n.Length>16)n=n.Substring(0,16)+"…";
            sharedStateLabel.text+=(row+1).ToString()+"位  "+n+"    "+Format(scores[i])+" 個\n";
        }
        if(count==0)sharedStateLabel.text="スコアを集計しています";
        sharedTurnLabel.text="累計の焼き上がり / 約3秒ごとに更新   "+(boardPage+1).ToString()+" / "+pages.ToString();
    }
}

