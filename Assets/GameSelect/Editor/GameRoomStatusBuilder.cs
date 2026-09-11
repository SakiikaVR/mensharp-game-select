using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class GameRoomStatusBuilder
{
    public static void Configure(GameRoomSession room)
    {
        var child=room.transform.Find("CenterStatusMonitor");
        if(child==null)
        {
            var go=new GameObject("CenterStatusMonitor");child=go.transform;child.SetParent(room.transform,false);
            child.localPosition=new Vector3(0,3.95f,0);
            var m=go.AddComponent<GameRoomStatusMonitor>();
            var screen=new GameObject("Screen",typeof(RectTransform),typeof(Canvas));screen.layer=8;
            var rect=screen.GetComponent<RectTransform>();rect.SetParent(child,false);rect.sizeDelta=new Vector2(1600,900);rect.localScale=Vector3.one*.00175f;
            var canvas=screen.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var bg=screen.AddComponent<Image>();bg.color=new Color32(29,46,40,255);bg.raycastTarget=false;
            m.screen=rect;
            Label(rect,"Heading","TABLE STATUS",350,90,42,new Color32(196,215,185,255));
            m.pileLabel=Label(rect,"Main","GAME TABLE",220,170,70,Color.white);
            m.stateLabel=Label(rect,"Details","対戦が始まると場の状態を表示します",-55,420,44,Color.white);
            m.turnLabel=Label(rect,"Turn","外側のモニターからゲームを選択",-355,110,44,new Color32(214,226,190,255));
        }
        var monitor=child.GetComponent<GameRoomStatusMonitor>();
        monitor.pileLabel.supportRichText=false;monitor.stateLabel.supportRichText=false;monitor.turnLabel.supportRichText=false;
        var roots=new List<GameObject>();var piles=new List<Text>();var states=new List<Text>();var turns=new List<Text>();
        var panels=room.GetComponentsInChildren<GameSelectPanel>(true);
        if(panels.Length>0)foreach(var game in panels[0].gameRoots)
        {
            if(game==null)continue;
            foreach(var proxy in game.GetComponents<MenSharp.MenSharpBehaviour>())
            {
                var type=proxy.GetType();var a=type.GetField("sharedPileLabel");var b=type.GetField("sharedStateLabel");var c=type.GetField("sharedTurnLabel");
                if(a==null||b==null||c==null||a.FieldType!=typeof(Text)||b.FieldType!=typeof(Text)||c.FieldType!=typeof(Text))continue;
                roots.Add(game);piles.Add((Text)a.GetValue(proxy));states.Add((Text)b.GetValue(proxy));turns.Add((Text)c.GetValue(proxy));
            }
        }
        monitor.sourceRoots=roots.ToArray();monitor.sourcePiles=piles.ToArray();monitor.sourceStates=states.ToArray();monitor.sourceTurns=turns.ToArray();
        EditorUtility.SetDirty(monitor);PrefabUtility.RecordPrefabInstancePropertyModifications(monitor);
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{child.gameObject},false);
    }
    private static Text Label(Transform parent,string name,string value,float y,float height,int size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.layer=8;
        var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=new Vector2(1500,height);r.anchoredPosition=new Vector2(0,y);
        var t=go.GetComponent<Text>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/GameSelect/Fonts/NotoSansJP-Bold.otf");t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=color;t.text=value;t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }
}
