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
        if (p.settings == null || (int)p.settings["defaultRules"] < 0 || (int)p.settings["defaultRules"] > 2047)
            throw new ArgumentException("Daifugo defaultRules must be an 11-bit mask.");
    }
    public static GameObject Create(GameSelectBuilder.Package p, Transform parent)
    {
        font = AssetDatabase.LoadAssetAtPath<Font>(GameSelectBuilder.Root+"/Fonts/NotoSansJP-Bold.otf");
        round = AssetDatabase.LoadAssetAtPath<Sprite>(GameSelectBuilder.Root+"/Art/round.png");
        var root = Rect(p.id,parent,0,0,1920,1080);
        var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color32(237,243,238,255);
        var g = root.gameObject.AddComponent<DaifugoGame>(); g.rules=(int)p.settings["defaultRules"];
        Text("Heading",root,"大 富 豪",-700,456,420,70,44);
        Text("Subtitle",root,"DAIFUGO  /  2–5 PLAYERS",-605,392,600,38,21);
        var gear = Button("Settings",root,630,458,170,70,"    設定");
        var gearIcon=Rect("Gear",gear.transform,-48,0,34,34).gameObject.AddComponent<Image>();gearIcon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(p.packageDirectory+"/Images/gear.png");gearIcon.raycastTarget=false;
        var back = Button("Back",root,820,458,180,70,"← 戻る");
        var join = Button("Join",root,-360,450,180,68,"参加");g.joinButton=join;
        var leave = Button("Leave",root,-160,450,180,68,"退出");g.leaveButton=leave;
        var start = Button("Start",root,90,450,280,68,"配札 / 次の対戦");g.startButton=start;
        var book=Button("RuleBook",root,374,458,225,70,"ルールブック");
        var cpu=Button("AddCpu",root,45,373,215,58,"CPUを追加");g.addCpuButton=cpu;
        var removeCpu=Button("RemoveCpu",root,288,373,240,58,"CPUを減らす");g.removeCpuButton=removeCpu;
        g.playersLabel=Text("Players",root,"参加者を待っています",-575,168,680,335,27);
        var field = Panel("Table",root,345,154,1010,335,new Color32(218,231,221,255));
        g.tableLabel=Text("Pile",field,"場にカードはありません",0,30,960,250,31);
        g.statusLabel=Text("Status",root,"参加を押してください（2～5人）",0,-69,1790,105,29);
        g.handLabel=Text("Hand",root,"あなたの手札",0,-157,1650,46,24);
        g.cardButtons=new Button[53];g.cardLabels=new Text[53];
        for(int c=0;c<53;c++)
        {
            var b=Button("Card"+c,root,0,-230,88,70,"?");g.cardButtons[c]=b;g.cardLabels[c]=b.GetComponentInChildren<Text>();
            g.cardLabels[c].fontSize=27;g.cardLabels[c].resizeTextMaxSize=27;
        }
        g.playButton=Button("PlayCards",root,565,-469,320,76,"出す / 効果を確定");
        g.passButton=Button("Pass",root,820,-469,170,76,"パス");
        g.hintLabel=Text("Hint",root,"カードを選択してください",-350,-471,1050,65,20);
        g.bombPanel=Panel("BombChoice",root,0,-307,1810,192,new Color32(230,218,200,255)).gameObject;
        Text("Heading",g.bombPanel.transform,"12ボンバー：全員の手札から取り除く数字",0,59,1600,40,25);
        g.bombButtons=new Button[13];
        for(int r=0;r<13;r++)
        {
            string label=r==8?"J":r==9?"Q":r==10?"K":r==11?"A":r==12?"2":(r+3).ToString();
            g.bombButtons[r]=Button("Bomb"+r,g.bombPanel.transform,-780+r*130,-27,112,78,label);
        }
        g.settingsPanel=Panel("SettingsPanel",root,0,0,1820,1010,new Color32(248,248,241,255)).gameObject;
        Text("Heading",g.settingsPanel.transform,"対戦ルール",-590,430,570,70,40);
        var close=Button("CloseSettings",g.settingsPanel.transform,740,430,220,68,"閉じる");
        string[] names={"10捨て","12ボンバー","Jバック","8切り","縛り","革命","階段","階段革命","スペ3返し","7渡し","5スキップ"};g.ruleNames=names;
        string[] descriptions={"10の枚数だけ手札を捨てる","Qの枚数だけ数字を指定し、全員から除去","Jを含むと、この場だけ強さを反転","8を含むと場を流す","同じスート構成が続くとスートを固定","同じ数字4枚以上で強さを反転","同一スートの連番3枚以上を出せる","階段4枚以上で強さを反転","単体ジョーカーにスペード3を出して流す","7の枚数だけ次の相手へ手札を渡す","5の枚数だけ順番を飛ばす"};
        g.ruleButtons=new Button[11];g.ruleLabels=new Text[11];
        for(int i=0;i<11;i++)
        {
            float x=i<6?-435:435, y=310-(i%6)*105;
            g.ruleButtons[i]=Button("Rule"+i,g.settingsPanel.transform,x,y,795,60,"ON "+names[i]);g.ruleLabels[i]=g.ruleButtons[i].GetComponentInChildren<Text>();
            Text("Description"+i,g.settingsPanel.transform,descriptions[i],x,y-41,820,36,20);
        }
        var lobby=Button("Lobby",g.settingsPanel.transform,435,-236,790,60,"結果画面から待機室へ（ルール変更・参加受付）");
        var voice=Button("Voice",g.settingsPanel.transform,-550,-360,450,65,"解説 ON / OFF");
        var sounds=Button("Sounds",g.settingsPanel.transform,0,-360,450,65,"効果音 ON / OFF");
        g.settingsLabel=Text("SettingsStatus",g.settingsPanel.transform,"VOICEVOX:ずんだもん",0,-448,1670,92,21);
        g.bookPanel=Panel("RuleBookPanel",root,0,0,1820,1010,new Color32(248,248,241,255)).gameObject;
        Text("Heading",g.bookPanel.transform,"大富豪  /  ルールブック",-300,431,1050,65,36);
        var bookClose=Button("Close",g.bookPanel.transform,744,430,200,65,"閉じる");
        g.bookLabel=Text("Page",g.bookPanel.transform,"",0,15,1620,725,29);
        var previous=Button("Previous",g.bookPanel.transform,-450,-427,370,65,"← 前のページ");
        var next=Button("Next",g.bookPanel.transform,450,-427,370,65,"次のページ →");
        g.bookPages=new[]{
            "基本の遊び方\n\n参加 → 配札で開始。1人ならCPUが1人加わります。\n待機中にCPUを追加・削除でき、人間とCPUで最大5人です。\n\n3が最弱、2が最強。ジョーカーは単体なら最強です。\n同じ枚数・同じ形で、場より強いカードを出します。\n同じ数字の組、またはONなら同一スートの階段を出せます。\n\n手札をクリックして選択し「出す / 効果を確定」を押します。\nパスすると、その場が流れるまで復帰できません。\n最後に出した人へ順番が戻ると場が流れます。\n手札をなくした順に順位が決まります。反則上がりはありません。",
            "特殊ルール  1\n\n10捨て：10の枚数だけ自分の手札を選んで捨てます。\n7渡し：7の枚数だけ次の未上がりの相手へ渡します。\n手札が足りない場合は、残っている分だけ選びます。\n\n12ボンバー：Qの枚数だけ数字を指定します。\n指定した数字を全員の手札から取り除きます。ジョーカーは対象外。\n同時に手札がなくなったら、出した人から席順で順位を決めます。\n\n5スキップ：5の枚数だけ次の順番を飛ばします。\n8切り：8を含むと場が流れ、出した人が続けて出せます。\nその人が上がっていたら、次の未上がりの人から再開します。",
            "特殊ルール  2\n\nJバック：Jを含むと場が流れるまで強さを反転。重ねると再反転。\n革命：同じ数字4枚以上で強さを反転。次の革命まで続きます。\n革命とJバックが両方有効なら通常の強さに戻ります。\n\n階段：同じスートの連番3枚以上。2と3はつながりません。\n階段革命：4枚以上の階段で革命。階段OFF時は発動しません。\n\n縛り：同じスート構成が続くと、その構成だけ出せます。\n場が流れると解除。数字縛り・激縛りはありません。\n\nスペ3返し：単体ジョーカーをスペード3で返し、場を流します。\n縛り中でも返せます。ジョーカーを含む複数枚には使えません。",
            "効果の重なり・ジョーカー\n\n特殊効果は実際に出したカードの数字だけで発動します。\nジョーカーを7・8・10・J・Qの代わりにしても、その効果は出ません。\nジョーカーは組の不足1枚、階段の欠け1枚を補えます。\n\n複数効果は 7渡し → 10捨て → 12ボンバー の順に選択します。\nすべて処理してから上がりを判定し、次の手番へ進みます。\n8切り・スペ3返しはこれらの処理後に場を流します。\n場を流す場合、5スキップは適用しません。\n\n5スキップは既にパスした人・上がった人を数えません。\n一周して自分の手番になる場合もあります。\n効果の選択中はパスできません。",
            "次の対戦・設定・音声\n\n次の対戦は最下位から開始します。初回はダイヤ3の所持者から。\n前回の1位と最下位は2枚、4人以上では2位と下から2位は1枚交換。\n下位が最強のカードを自動で渡し、上位が返すカードを選びます。\n\n歯車から11ルールをON/OFF。初期値はすべてONです。\nルール変更は待機中・最初の人間の参加者のみ。全員に同期します。\n結果画面で待機室へ戻ると、参加受付とルール変更ができます。\n\n解説ONで、発動した効果をずんだもんが読み上げます。\n解説は初期OFF、効果音は初期ON。音声設定は自分だけに反映します。\nCPUは自分の手札と場を使い、約1秒おきに判断します。\nVOICEVOX:ずんだもん"
        };
        g.effectsSource=root.gameObject.AddComponent<AudioSource>();g.effectsSource.playOnAwake=false;g.effectsSource.spatialBlend=0f;g.effectsSource.volume=.35f;
        g.voiceSource=root.gameObject.AddComponent<AudioSource>();g.voiceSource.playOnAwake=false;g.voiceSource.spatialBlend=0f;g.voiceSource.volume=.65f;
        string audio=p.packageDirectory+"/Audio/";
        g.turnSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"turn.wav");g.cardSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"card.wav");g.passSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"pass.wav");g.winSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"win.wav");
        g.ruleVoices=new AudioClip[11];for(int i=0;i<11;i++)g.ruleVoices[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"Voice/rule"+i+".wav");
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{root.gameObject},false);
        var u=root.GetComponent<UdonBehaviour>();if(u==null)throw new Exception("Compile DaifugoGame first.");g.self=u;g.table=u;g.roomViews=new[]{u};
        Wire(join,u,"Join");Wire(leave,u,"Leave");Wire(start,u,"Begin");Wire(gear,u,"Settings");Wire(close,u,"Settings");Wire(back,u,"BackToMenu");
        Wire(g.playButton,u,"Play");Wire(g.passButton,u,"Pass");Wire(voice,u,"ToggleVoice");Wire(sounds,u,"ToggleSound");Wire(lobby,u,"Lobby");
        Wire(cpu,u,"AddComputer");Wire(removeCpu,u,"RemoveComputer");Wire(book,u,"RuleBook");Wire(bookClose,u,"RuleBook");Wire(previous,u,"PreviousPage");Wire(next,u,"NextPage");
        for(int i=0;i<53;i++)Wire(g.cardButtons[i],u,"Card"+i);
        for(int i=0;i<11;i++)Wire(g.ruleButtons[i],u,"Rule"+i);
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
    {var t=Rect(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=16;t.resizeTextMaxSize=size;return t;}
    private static Button Button(string n,Transform p,float x,float y,float w,float h,string value)
    {var r=Panel(n,p,x,y,w,h,Color.white);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;Text("Label",r,value,0,0,w-14,h-8,26);return b;}
}
