using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

public static class OmikujiPackageBuilder
{
    [Serializable] public class Settings
    {
        public string[] messages, luckyItems;
        public string revealSound;
    }
    private static string art;
    private static readonly Color Ink = new Color32(43,38,36,255);
    private static readonly Color Red = new Color32(163,57,49,255);
    public static void ValidatePackage(GameSelectBuilder.Package package)
    {
        var s=package.settings?.ToObject<Settings>();
        if(s==null || s.messages==null || s.messages.Length==0 || s.messages.Length>64 || s.luckyItems==null || s.luckyItems.Length==0 || s.luckyItems.Length>64)
            throw new ArgumentException("Omikuji requires 1–64 messages and luckyItems.");
        foreach(var m in s.messages) if(string.IsNullOrWhiteSpace(m)||m.Length>70)throw new ArgumentException("Omikuji message must contain 1–70 characters.");
        foreach(var m in s.luckyItems) if(string.IsNullOrWhiteSpace(m)||m.Length>24)throw new ArgumentException("Omikuji lucky item must contain 1–24 characters.");
        if(AssetDatabase.LoadAssetAtPath<AudioClip>(GameSelectBuilder.ResolvePackageAsset(package,s.revealSound))==null)throw new ArgumentException("Missing reveal sound.");
    }
    public static GameObject Create(GameSelectBuilder.Package package,Transform parent)
    {
        art=package.packageDirectory+"/Images/";
        var root=Rect(package.id,parent,0,0,1920,1080);
        var bg=root.gameObject.AddComponent<Image>();bg.color=new Color32(249,246,240,255);bg.raycastTarget=true;
        var game=root.gameObject.AddComponent<GentleOmikujiGame>();
        Label("Heading",root,"おみくじ",-530,450,760,70,42);
        Label("Subtitle",root,"本日の運勢",-540,382,760,45,25);
        var back=Button("Back",root,700,442,360,76,"← ゲーム選択へ",false);
        Image("Rule",root,0,330,1750,2,null,new Color32(213,200,181,255));
        var shrine=Image("Shrine",root,-475,34,380,380,"shrine",Color.white);game.shrine=shrine.rectTransform;
        Label("Promise",root,"心を落ち着けて\nお引きください",-470,-260,670,135,38);
        Label("SmallPrint",root,"ボタンを押して運勢を占います",-470,-395,760,55,23);
        var border=Image("PaperBorder",root,420,8,760,610,"round",Red);
        Image("Paper",border.transform,0,0,748,598,"round",Color.white);
        Label("PaperTitle",border.transform,"御 神 籤",0,245,650,45,25);
        game.rankLabel=Label("Rank",border.transform,"福",0,117,660,172,125);game.rankLabel.color=Red;
        Image("PaperRule",border.transform,0,12,585,2,null,new Color32(225,203,189,255));
        game.messageLabel=Label("Message",border.transform,"あなたの運勢を占います",0,-68,650,115,31);
        game.luckyLabel=Label("Lucky",border.transform,"おみくじを引いてください",0,-212,650,90,25);
        game.drawButton=Button("Draw",root,420,-370,680,104,"おみくじを引く",true);
        game.drawLabel=game.drawButton.GetComponentInChildren<Text>();
        game.countLabel=Label("Count",root,"抽選回数 0 回",420,-473,700,45,22);
        var settings=package.settings.ToObject<Settings>();game.messages=settings.messages;game.luckyItems=settings.luckyItems;
        game.sound=root.gameObject.AddComponent<AudioSource>();game.sound.playOnAwake=false;game.sound.spatialBlend=0f;game.sound.volume=.2f;
        game.revealSound=AssetDatabase.LoadAssetAtPath<AudioClip>(GameSelectBuilder.ResolvePackageAsset(package,settings.revealSound));
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{root.gameObject},false);
        var u=root.GetComponent<UdonBehaviour>();if(u==null)throw new InvalidOperationException("Compile GentleOmikujiGame first.");
        UnityEventTools.AddStringPersistentListener(game.drawButton.onClick,u.SendCustomEvent,"DrawFortune");
        UnityEventTools.AddStringPersistentListener(back.onClick,u.SendCustomEvent,"BackToMenu");
        root.gameObject.SetActive(false);return root.gameObject;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=8;var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static Text Label(string name,Transform parent,string value,float x,float y,float w,float h,int size)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=AssetDatabase.LoadAssetAtPath<Font>(GameSelectBuilder.Root+"/Fonts/NotoSansJP-Bold.otf");t.text=value;t.fontSize=size;t.color=Ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=18;t.resizeTextMaxSize=size;return t;
    }
    private static Image Image(string name,Transform parent,float x,float y,float w,float h,string sprite,Color color)
    {
        var i=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();i.raycastTarget=false;i.color=color;
        if(sprite!=null){i.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+sprite+".png");if(sprite=="round")i.type=UnityEngine.UI.Image.Type.Sliced;}return i;
    }
    private static Button Button(string name,Transform parent,float x,float y,float w,float h,string value,bool accent)
    {
        var i=Image(name,parent,x,y,w,h,"round",accent?Red:Color.white);i.raycastTarget=true;
        var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;var n=b.navigation;n.mode=Navigation.Mode.None;b.navigation=n;
        var t=Label("Label",i.transform,value,0,0,w-24,h-10,32);t.color=accent?Color.white:Ink;return b;
    }
}
