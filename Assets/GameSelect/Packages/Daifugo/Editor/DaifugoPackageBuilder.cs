using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using VRC.Udon;

public static class DaifugoPackageBuilder
{
    private static Font font;
    private static Sprite round;
    private static Color ink = new Color32(33,48,43,255);
    public static void ValidatePackage(GameSelectBuilder.Package p)
    {
        if (p.settings == null || (int)p.settings["defaultRules"] < 0 || (int)p.settings["defaultRules"] > 4095)
            throw new ArgumentException("Daifugo defaultRules must be an 12-bit mask.");
    }
    public static GameObject Create(GameSelectBuilder.Package p, Transform parent)
    {
        font = AssetDatabase.LoadAssetAtPath<Font>(GameSelectBuilder.Root+"/Fonts/NotoSansJP-Bold.otf");
        round = AssetDatabase.LoadAssetAtPath<Sprite>(GameSelectBuilder.Root+"/Art/round.png");
        var root = Rect(p.id,parent,0,0,1920,1080);
        var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color32(237,243,238,255);
        var g = root.gameObject.AddComponent<DaifugoGame>(); g.rules=(int)p.settings["defaultRules"];
        var shared = Rect("SharedTableDisplay",root,0,0,1,1);
        g.sharedPileLabel=Text("Pile",shared,"大富豪 / 参加待ち",0,0,1,1,40);
        g.sharedStateLabel=Text("State",shared,"縛り：なし    Jバック：なし\n革命：なし",0,0,1,1,40);
        g.sharedTurnLabel=Text("Turn",shared,"参加 → 配札で開始",0,0,1,1,40);
        shared.gameObject.SetActive(false);
        Text("Heading",root,"大 富 豪",-770,459,310,90,60);
        Text("Subtitle",root,"1〜4人 / 空席はCPU",-670,373,520,52,34);
        var gear = Button("Settings",root,645,458,200,92,"    設定");
        var gearIcon=Rect("Gear",gear.transform,-48,0,34,34).gameObject.AddComponent<Image>();gearIcon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(p.packageDirectory+"/Images/gear.png");gearIcon.raycastTarget=false;
        var back = Button("Back",root,845,458,170,92,"← 戻る");
        var join = Button("Join",root,-480,458,180,92,"参加");g.joinButton=join;
        var leave = Button("Leave",root,-285,458,180,92,"退出");g.leaveButton=leave;
        var start = Button("Start",root,20,458,390,92,"配札 / 次の対戦");g.startButton=start;
        var book=Button("RuleBook",root,385,458,280,92,"ルールブック");
        var cpu=Button("AddCpu",root,450,356,450,80,"CPU補充 ON");g.addCpuButton=cpu;
        
        g.playersLabel=Text("Players",root,"参加者を待っています",-667,177,505,255,38);
        g.playersLabel.gameObject.SetActive(false);
        var field = Panel("Table",root,0,100,1800,105,new Color32(218,231,221,255));
        g.tableLabel=Text("Pile",field,"場にカードはありません",0,0,1760,101,40);
        g.opponentTiles=new GameObject[4];g.opponentNames=new Text[4];g.opponentCounts=new Text[4];
        for(int i=0;i<4;i++)
        {
            var tile=Panel("Opponent"+i,root,0,238,550,160,Color.white);g.opponentTiles[i]=tile.gameObject;
            var backImage=Rect("CardBack",tile,-170,0,76,114).gameObject.AddComponent<Image>();backImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(p.packageDirectory+"/Images/card-back.png");backImage.raycastTarget=false;
            g.opponentNames[i]=Text("Name",tile,"相手",35,43,335,62,38);
            g.opponentCounts[i]=Text("Remaining",tile,"残り 13 枚",35,-30,335,82,50);
        }
        g.statusLabel=Text("Status",root,"参加 → 配札で開始。空席はCPUが参加します",0,5,1820,70,42);
        g.handLabel=Text("Hand",root,"あなたの手札",0,-65,1740,50,36);
        var hand=Rect("HandCards",root,0,0,1920,1080);
        g.cardButtons=new Button[53];g.cardLabels=new Text[53];
        for(int c=0;c<53;c++)
        {
            var b=Button("Card"+c,hand,0,-230,150,224,"?");g.cardButtons[c]=b;g.cardLabels[c]=b.GetComponentInChildren<Text>();
            var face=b.image;face.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(p.packageDirectory+"/Images/card-face.png");face.type=Image.Type.Simple;
            var label=g.cardLabels[c];label.fontSize=40;label.resizeTextForBestFit=false;label.alignment=TextAnchor.UpperLeft;
            label.rectTransform.anchoredPosition=new Vector2(-33,0);label.rectTransform.sizeDelta=new Vector2(67,194);
            string mark=c==52?"★":c/13==0?"♠":c/13==1?"♥":c/13==2?"♦":"♣";
            var pip=Text("Suit",b.transform,mark,22,-30,90,110,70);pip.color=c<52&&(c/13==1||c/13==2)?new Color(.65f,.18f,.17f):ink;
        }
        g.playButton=Button("PlayCards",root,445,-464,560,112,"出す / 確定");
        g.passButton=Button("Pass",root,833,-464,185,112,"パス");
        g.hintLabel=Text("Hint",root,"カードを選択してください",-474,-472,800,80,34);
        g.bombPanel=Panel("BombChoice",root,0,-307,1810,192,new Color32(230,218,200,255)).gameObject;
        Text("Heading",g.bombPanel.transform,"12ボンバー：全員の手札から取り除く数字",0,59,1600,40,25);
        g.bombButtons=new Button[13];
        for(int r=0;r<13;r++)
        {
            string label=r==8?"J":r==9?"Q":r==10?"K":r==11?"A":r==12?"2":(r+3).ToString();
            g.bombButtons[r]=Button("Bomb"+r,g.bombPanel.transform,-780+r*130,-27,112,78,label);
        }
        g.settingsPanel=Panel("SettingsPanel",root,0,0,1820,1010,new Color32(248,248,241,255)).gameObject;
        Text("Heading",g.settingsPanel.transform,"対戦ルール",-590,430,570,80,54);
        var close=Button("CloseSettings",g.settingsPanel.transform,740,430,220,68,"閉じる");
        string[] names={"10捨て","12ボンバー","Jバック","8切り","縛り","革命","階段","階段革命","スペ3返し","7渡し","5スキップ","禁止上がり"};g.ruleNames=names;
        string[] descriptions={"10の枚数だけ手札を捨てる","Qの枚数だけ数字を指定し、全員から除去","Jを含むと、この場だけ強さを反転","8を含むと場を流す","同じスート構成が続くとスートを固定","同じ数字4枚以上で強さを反転","同一スートの連番3枚以上を出せる","階段4枚以上で強さを反転","単体ジョーカーにスペード3を出して流す","7の枚数だけ次の相手へ手札を渡す","5の枚数だけ順番を飛ばす","2（革命中3）・8・JOKERは反則上がり"};
        g.ruleButtons=new Button[12];g.ruleLabels=new Text[12];
        for(int i=0;i<12;i++)
        {
            float x=i<6?-435:435, y=335-(i%6)*116;
            g.ruleButtons[i]=Button("Rule"+i,g.settingsPanel.transform,x,y,850,78,"ON "+names[i]);g.ruleLabels[i]=g.ruleButtons[i].GetComponentInChildren<Text>();
            Text("Description"+i,g.settingsPanel.transform,descriptions[i],x,y-48,850,42,29);
        }
        var lobby=Button("Lobby",g.settingsPanel.transform,565,-360,515,85,"待機室に戻る");
        var voice=Button("Voice",g.settingsPanel.transform,-595,-360,485,85,"解説 ON / OFF");
        var sounds=Button("Sounds",g.settingsPanel.transform,-75,-360,485,85,"効果音 ON / OFF");
        g.settingsLabel=Text("SettingsStatus",g.settingsPanel.transform,"VOICEVOX:ずんだもん",0,-458,1720,95,29);
        g.bookPanel=Panel("RuleBookPanel",root,0,0,1820,1010,new Color32(248,248,241,255)).gameObject;
        Text("Heading",g.bookPanel.transform,"大富豪  /  ルールブック",-300,431,1050,80,54);
        var bookClose=Button("Close",g.bookPanel.transform,744,430,200,65,"閉じる");
        g.bookLabel=Text("Page",g.bookPanel.transform,"",0,25,1750,735,48);
        var previous=Button("Previous",g.bookPanel.transform,-490,-440,550,95,"← 前のページ");
        var next=Button("Next",g.bookPanel.transform,490,-440,550,95,"次のページ →");
        g.bookPages=new[]{
            "はじめ方\n\n参加できる人間は1〜4人。\n「参加」→「配札」で開始します。\nCPU補充ONなら、空席をCPUが埋めて4人戦。\nCPU補充OFFなら、人間2〜4人で対戦します。",
            "カードの出し方\n\n手札は扇状に並びます。押すと選択されます。\n選択したカードが上に持ち上がります。\n「出す / 確定」で提出、「パス」で見送ります。\n場と同じ枚数・同じ形で、より強い札を出します。",
            "強さと場の流れ\n\n通常は3が最弱、2が最強。ジョーカーは単体最強。\nパスした人は、その場が流れるまで復帰できません。\n最後に出した人へ戻ると場が流れます。\n手札をなくした順に順位が決まります。",
            "10捨て / 7渡し\n\n10：出した枚数だけ、自分の手札を選んで捨てます。\n7：出した枚数だけ、次の相手へ手札を渡します。\nどちらもカードを選んで「出す / 確定」。\n手札が足りない場合は残りすべてを選びます。",
            "12ボンバー / 5スキップ\n\nQ：数字を指定し、全員からその数字を除きます。\nQの枚数だけ指定できます。JOKERは対象外です。\n5：出した枚数だけ未パスの相手を飛ばします。\n一周して自分の手番になることもあります。",
            "8切り / スペ3返し\n\n8を含む札を出すと、場が流れます。\nその人が上がっていたら、次の人から再開します。\n単体ジョーカーにはスペード3で返せます。\nスペ3返しは縛りを無視して場を流します。",
            "革命 / Jバック\n\n同じ数字4枚以上で革命。強さが反転します。\nJを含むと、この場だけ強さを反転します。\nJバックは場が流れると解除されます。\n革命とJバックが重なると通常の強さです。",
            "階段 / 階段革命 / 縛り\n\n同一スートの連番3枚以上が階段です。\n階段4枚以上で革命。2と3はつながりません。\n同じスート構成が続くと縛りが発動します。\n縛られたスート構成以外は出せません。",
            "禁止上がり（初期ON）\n\n通常の2・革命中の3・8・ジョーカーでは\n最後の手札を出して上がれません。\n出した場合は反則負けとなり、下位順位が確定します。\nJバックだけでは禁止対象の2と3は入れ替わりません。",
            "効果が重なったら\n\n7渡し → 10捨て → 12ボンバー の順に選びます。\nその後で上がり・場の流れ・次の手番を処理します。\n場を流す効果は5スキップより優先されます。\nジョーカーが代用した数字の効果は発動しません。",
            "次の対戦\n\n初回はダイヤ3の所持者、次は前回最下位から。\n1位と最下位は2枚、4人戦の2位と3位は1枚交換。\n下位は強い札を自動で渡します。\n上位は返す札を選んで「出す / 確定」。",
            "設定と音声\n\n歯車から12ルールを切り替えます。初期はすべてON。\nルールとCPU設定は待機中にホストが変更します。\n解説ONで効果を読み上げます。音声設定は自分だけ。\n音声：VOICEVOX:ずんだもん"
        };
        g.effectsSource=root.gameObject.AddComponent<AudioSource>();g.effectsSource.playOnAwake=false;g.effectsSource.spatialBlend=0f;g.effectsSource.volume=.35f;
        g.voiceSource=root.gameObject.AddComponent<AudioSource>();g.voiceSource.playOnAwake=false;g.voiceSource.spatialBlend=0f;g.voiceSource.volume=.65f;
        string audio=p.packageDirectory+"/Audio/";
        g.turnSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"turn.wav");g.cardSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"card.wav");g.passSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"pass.wav");g.winSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"win.wav");
        g.ruleVoices=new AudioClip[12];for(int i=0;i<12;i++)g.ruleVoices[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"Voice/rule"+i+".wav");
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{root.gameObject},false);
        var u=root.GetComponent<UdonBehaviour>();if(u==null)throw new Exception("Compile DaifugoGame first.");g.self=u;g.table=u;g.roomViews=new[]{u};
        Wire(join,u,"Join");Wire(leave,u,"Leave");Wire(start,u,"Begin");Wire(gear,u,"Settings");Wire(close,u,"Settings");Wire(back,u,"BackToMenu");
        Wire(g.playButton,u,"Play");Wire(g.passButton,u,"Pass");Wire(voice,u,"ToggleVoice");Wire(sounds,u,"ToggleSound");Wire(lobby,u,"Lobby");
        Wire(cpu,u,"AddComputer");Wire(book,u,"RuleBook");Wire(bookClose,u,"RuleBook");Wire(previous,u,"PreviousPage");Wire(next,u,"NextPage");
        for(int i=0;i<53;i++)Wire(g.cardButtons[i],u,"Card"+i);
        for(int i=0;i<12;i++)Wire(g.ruleButtons[i],u,"Rule"+i);
        for(int i=0;i<13;i++)Wire(g.bombButtons[i],u,"Bomb"+i);
        g.settingsPanel.SetActive(false);g.bombPanel.SetActive(false);g.bookPanel.SetActive(false);for(int i=0;i<53;i++)g.cardButtons[i].gameObject.SetActive(false);
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{root.gameObject},false);
        root.gameObject.SetActive(false);return root.gameObject;
    }
    private static void Wire(Button b,UdonBehaviour u,string name){UnityEventTools.AddStringPersistentListener(b.onClick,u.SendCustomEvent,name);}
    private static RectTransform Rect(string n,Transform p,float x,float y,float w,float h)
    {var o=new GameObject(n,typeof(RectTransform));o.layer=8;var r=o.GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
    private static RectTransform Panel(string n,Transform p,float x,float y,float w,float h,Color color)
    {var r=Rect(n,p,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.sprite=round;i.type=Image.Type.Sliced;i.color=color;return r;}
    private static Text Text(string n,Transform p,string value,float x,float y,float w,float h,int size)
    {var t=Rect(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Min(size,32);t.resizeTextMaxSize=size;return t;}
    private static Button Button(string n,Transform p,float x,float y,float w,float h,string value)
    {var r=Panel(n,p,x,y,w,h,Color.white);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;Text("Label",r,value,0,0,w-18,h-8,40);return b;}
}
