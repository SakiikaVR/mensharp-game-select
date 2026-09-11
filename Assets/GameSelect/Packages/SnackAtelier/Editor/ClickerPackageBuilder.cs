using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

// Package data is baked into ordinary MenSharp/Udon fields before building the world.
public static class ClickerPackageBuilder
{
    [Serializable] public class Rules
    {
        public float initialClickPower = 1, costMultiplier = 1.18f;
        public Upgrade[] upgrades;
        public string clickSound, purchaseSound;
    }
    [Serializable] public class Upgrade
    {
        public string name;
        public float cost, clickGain, productionGain;
    }
    public static void Validate(Rules rules)
    {
        if (rules == null || rules.upgrades == null || rules.upgrades.Length < 1 || rules.upgrades.Length > 4)
            throw new ArgumentException("clicker.upgrades must contain 1–4 upgrades.");
        if (!Finite(rules.initialClickPower) || rules.initialClickPower <= 0 || !Finite(rules.costMultiplier) || rules.costMultiplier < 1.01f || rules.costMultiplier > 10)
            throw new ArgumentException("Invalid click power or cost multiplier.");
        foreach (var u in rules.upgrades)
            if (u == null || string.IsNullOrWhiteSpace(u.name) || u.name.Length > 20 || !Finite(u.cost) || u.cost < 1 || !Finite(u.clickGain) || !Finite(u.productionGain) || u.clickGain < 0 || u.productionGain < 0 || u.clickGain + u.productionGain <= 0)
                throw new ArgumentException("Invalid clicker upgrade name, cost, or gain.");
    }
    private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    public static void ValidatePackage(GameSelectBuilder.Package package)
    {
        var rules = package.clicker?.ToObject<Rules>();
        Validate(rules);
        foreach (var path in new[] { rules.clickSound, rules.purchaseSound })
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<AudioClip>(GameSelectBuilder.ResolvePackageAsset(package, path)) == null)
                throw new ArgumentException("Missing package audio: " + path);
    }
    private static string packageArt;
    public static GameObject Create(GameSelectBuilder.Package package, Transform parent)
    {
        packageArt = package.packageDirectory + "/Images/";
        var root = Rect(package.id, parent, 0, 0, 1920, 1080);
        var game = root.gameObject.AddComponent<CookieClickerGame>();
        // Opaque raycast target prevents clicks leaking into the selection panel.
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color32(246,245,242,255); background.raycastTarget = true;
        Text("Heading",root,"SNACK ATELIER",-520,451,740,60,40);
        Text("Subtitle",root,package.title,-525,390,730,45,25);
        var back = Button("Back",root,710,445,340,72,"←  ゲーム選択へ");
        Image("Rule",root,0,345,1750,2,null,new Color32(205,204,199,255));
        game.balanceLabel = Text("Balance",root,"0",-445,225,830,110,88);
        Text("Unit",root,"焼 き 上 が り",-445,143,650,42,23);
        game.rateLabel = Text("Rate",root,"1 / CLICK  ·  0 / SEC",-445,83,800,45,24);
        var cookieImage = Image("Cookie",root,-445,-155,365,365,"cookie",Color.white);
        game.cookie = cookieImage.rectTransform;
        var bake = cookieImage.gameObject.AddComponent<Button>(); bake.targetGraphic = cookieImage; cookieImage.raycastTarget = true;
        var nav = bake.navigation; nav.mode=Navigation.Mode.None; bake.navigation=nav;
        game.feedbackLabel = Text("Feedback",root,"タップして、おやつを焼こう！",-445,-394,850,50,27);
        game.totalLabel = Text("Total",root,"これまでの焼き上がり  0",-445,-469,850,36,20);
        Text("ShopHeading",root,"工房を育てる",465,273,780,60,32);
        var rules = package.clicker.ToObject<Rules>();
        game.sound = root.gameObject.AddComponent<AudioSource>();
        game.sound.playOnAwake = false; game.sound.spatialBlend = 0; game.sound.volume = 0.2f;
        game.clickSound = AssetDatabase.LoadAssetAtPath<AudioClip>(GameSelectBuilder.ResolvePackageAsset(package, rules.clickSound));
        game.purchaseSound = AssetDatabase.LoadAssetAtPath<AudioClip>(GameSelectBuilder.ResolvePackageAsset(package, rules.purchaseSound));
        int n=rules.upgrades.Length;
        game.initialClickPower=rules.initialClickPower; game.costMultiplier=rules.costMultiplier;
        game.upgradeNames=new string[n];game.baseCosts=new float[n];game.clickGains=new float[n];game.productionGains=new float[n];
        game.shopButtons=new Button[n];game.shopLabels=new Text[n];
        for(int i=0;i<n;i++)
        {
            var u=rules.upgrades[i];game.upgradeNames[i]=u.name;game.baseCosts[i]=u.cost;game.clickGains[i]=u.clickGain;game.productionGains[i]=u.productionGain;
            var b=Button("Upgrade"+i,root,465,153-i*151,780,128,u.name+"\nCOST  "+u.cost);
            game.shopButtons[i]=b;game.shopLabels[i]=b.GetComponentInChildren<Text>();
        }
        Text("Session",root,"LOCAL SESSION  ·  戻っても進行を保持 / 終了時にリセット",460,-479,850,40,19);
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{root.gameObject},false);
        var backing=root.GetComponent<UdonBehaviour>();
        if(backing==null) throw new InvalidOperationException("CookieClickerGame MenSharp program is not compiled.");
        UnityEventTools.AddStringPersistentListener(bake.onClick,backing.SendCustomEvent,"Bake");
        UnityEventTools.AddStringPersistentListener(back.onClick,backing.SendCustomEvent,"BackToMenu");
        string[] methods={"BuyFirst","BuySecond","BuyThird","BuyFourth"};
        for(int i=0;i<n;i++)UnityEventTools.AddStringPersistentListener(game.shopButtons[i].onClick,backing.SendCustomEvent,methods[i]);
        root.gameObject.SetActive(false);
        return root.gameObject;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=8;
        var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*0.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static Text Text(string name,Transform parent,string value,float x,float y,float w,float h,int size)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=AssetDatabase.LoadAssetAtPath<Font>(GameSelectBuilder.Root+"/Fonts/NotoSansJP-Bold.otf");t.fontSize=size;t.text=value;t.color=new Color32(29,29,28,255);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=16;t.resizeTextMaxSize=size;return t;
    }
    private static Image Image(string name,Transform parent,float x,float y,float w,float h,string sprite,Color color)
    {
        var img=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();img.color=color;img.raycastTarget=false;
        if(sprite!=null)img.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(packageArt+sprite+".png");return img;
    }
    private static Button Button(string name,Transform parent,float x,float y,float w,float h,string value)
    {
        var image=Image(name,parent,x,y,w,h,"round",Color.white);image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=true;
        var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
        var colors=b.colors;colors.highlightedColor=new Color(.89f,.87f,.82f);colors.pressedColor=new Color(.72f,.68f,.6f);colors.disabledColor=new Color(.8f,.8f,.79f);b.colors=colors;
        Text("Label",image.transform,value,0,0,w-36,h-12,27);return b;
    }
}

