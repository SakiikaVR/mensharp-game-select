using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

public static class GameRoomSetup
{
    public const string RoomPrefabPath = "Assets/GameSelect/GameRoom.prefab";

    [MenuItem("Game Select/Create Four Monitor Room")]
    public static void CreateRoom()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var existing = Object.FindObjectOfType<GameRoomSession>();
        if (existing != null) { Rewire(existing); GameRoomFurniture.Configure(existing); Selection.activeGameObject=existing.gameObject; return; }
        MenSharpCompiler.CompileAll();
        var first = Object.FindObjectOfType<GameSelectPanel>();
        if (first == null) { GameSelectBuilder.CreatePanel(); first=Object.FindObjectOfType<GameSelectPanel>(); }
        var root = new GameObject("GameRoom");
        Undo.RegisterCreatedObjectUndo(root,"Create four monitor room");
        var session=root.AddComponent<GameRoomSession>();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(GameSelectBuilder.PrefabPath);
        Vector3[] positions={new Vector3(0,2.1f,4),new Vector3(4,2.1f,0),new Vector3(0,2.1f,-4),new Vector3(-4,2.1f,0)};
        string[] names={"GameSelectPanel","MonitorEast","MonitorSouth","MonitorWest"};
        for(int i=0;i<4;i++)
        {
            var go=i==0?first.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name=names[i];go.transform.SetParent(root.transform,true);
            go.transform.localPosition=positions[i];go.transform.localRotation=Quaternion.Euler(0,i*90,0);go.transform.localScale=Vector3.one*0.0025f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        Rewire(session);
        GameRoomFurniture.Configure(session);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root,RoomPrefabPath,InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject=root;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0,2,0),new Vector3(10,5,10)),false);
    }

    public static void Rewire(GameRoomSession session)
    {
        var panels=session.GetComponentsInChildren<GameSelectPanel>(true);
        if(panels.Length==0)return;
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{session.gameObject},false);
        var backing=session.GetComponent<UdonBehaviour>();
        if(backing==null)throw new System.InvalidOperationException("Compile GameRoomSession before wiring the room.");
        session.self=backing;
        session.monitors=new UdonBehaviour[panels.Length];
        session.installedGameIds=panels[0].gameIds;
        for(int i=0;i<panels.Length;i++)
        {
            var panel=panels[i];panel.sessionController=backing;panel.sessionGameId="";
            session.monitors[i]=panel.GetComponent<UdonBehaviour>();
            EnsurePlaceholder(panel);
            foreach(var game in panel.gameRoots)
            {
                if(game==null)continue;
                foreach(var proxy in game.GetComponents<MenSharp.MenSharpBehaviour>())
                {
                    // Optional package contract; no dependency on a particular game class.
                    var field=proxy.GetType().GetField("sessionController");
                    if(field!=null && field.FieldType==typeof(UdonBehaviour))
                    {field.SetValue(proxy,backing);EditorUtility.SetDirty(proxy);PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);}
                }
                MenSharpProxy.SyncThenTransfer(new List<GameObject>{game},false);
            }
            EditorUtility.SetDirty(panel);PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            MenSharpProxy.SyncThenTransfer(new List<GameObject>{panel.gameObject},false);
        }
        // Optional shared-table contract. Package classes remain removable.
        for(int gameIndex=0;gameIndex<panels[0].gameIds.Length;gameIndex++)
        {
            string id=panels[0].gameIds[gameIndex];
            var games=new List<GameObject>();
            foreach(var p in panels)
                for(int k=0;k<p.gameIds.Length;k++) if(p.gameIds[k]==id && p.gameRoots[k]!=null)games.Add(p.gameRoots[k]);
            var views=games.Select(g=>g.GetComponent<UdonBehaviour>()).ToArray();
            if(views.Length==0)continue;
            foreach(var game in games)
            {
                foreach(var proxy in game.GetComponents<MenSharp.MenSharpBehaviour>())
                {
                    var authority=proxy.GetType().GetField("table");var peers=proxy.GetType().GetField("roomViews");
                    if(authority==null || peers==null || authority.FieldType!=typeof(UdonBehaviour) || peers.FieldType!=typeof(UdonBehaviour[]))continue;
                    authority.SetValue(proxy,views[0]);peers.SetValue(proxy,views);
                    EditorUtility.SetDirty(proxy);PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
                }
                MenSharpProxy.SyncThenTransfer(new List<GameObject>{game},false);
            }
        }
        EditorUtility.SetDirty(session);PrefabUtility.RecordPrefabInstancePropertyModifications(session);
        MenSharpProxy.SyncThenTransfer(new List<GameObject>{session.gameObject},false);
        GameRoomStatusBuilder.Configure(session);
    }

    private static void EnsurePlaceholder(GameSelectPanel panel)
    {
        var t=panel.transform.Find("SessionPlaceholder");
        if(t==null)
        {
            var go=new GameObject("SessionPlaceholder",typeof(RectTransform),typeof(Image));go.layer=8;
            var r=go.GetComponent<RectTransform>();r.SetParent(panel.transform,false);r.sizeDelta=new Vector2(1920,1080);
            go.GetComponent<Image>().color=new Color32(247,247,245,255);
            var title=new GameObject("Title",typeof(RectTransform),typeof(Text));title.layer=8;
            var tr=title.GetComponent<RectTransform>();tr.SetParent(r,false);tr.sizeDelta=new Vector2(1600,340);tr.anchoredPosition=new Vector2(0,70);
            var label=title.GetComponent<Text>();label.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/GameSelect/Fonts/NotoSansJP-Bold.otf");label.fontSize=58;label.alignment=TextAnchor.MiddleCenter;label.color=new Color32(29,29,28,255);label.raycastTarget=false;
            var close=new GameObject("Return",typeof(RectTransform),typeof(Image),typeof(Button));close.layer=8;
            var cr=close.GetComponent<RectTransform>();cr.SetParent(r,false);cr.sizeDelta=new Vector2(650,130);cr.anchoredPosition=new Vector2(0,-260);
            var image=close.GetComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameSelect/Art/round.png");image.type=Image.Type.Sliced;
            var button=close.GetComponent<Button>();button.targetGraphic=image;
            var ct=new GameObject("Label",typeof(RectTransform),typeof(Text));ct.layer=8;var ctr=ct.GetComponent<RectTransform>();ctr.SetParent(cr,false);ctr.sizeDelta=cr.sizeDelta;
            var text=ct.GetComponent<Text>();text.font=label.font;text.fontSize=36;text.alignment=TextAnchor.MiddleCenter;text.color=label.color;text.text="全モニターをゲーム選択へ戻す";text.raycastTarget=false;
            UnityEventTools.AddStringPersistentListener(button.onClick,panel.GetComponent<UdonBehaviour>().SendCustomEvent,"CloseRoomGame");
            t=r;
        }
        panel.sessionPlaceholder=t.gameObject;panel.sessionTitle=t.GetComponentInChildren<Text>(true);
        t.SetAsLastSibling();t.gameObject.SetActive(false);
    }

    public static void RefreshRooms()
    {
        if(System.IO.File.Exists(RoomPrefabPath))
        {
            var root=PrefabUtility.LoadPrefabContents(RoomPrefabPath);
            try {Rewire(root.GetComponent<GameRoomSession>());PrefabUtility.SaveAsPrefabAsset(root,RoomPrefabPath);}
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var room in Object.FindObjectsOfType<GameRoomSession>(true))
            if(room.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(room.gameObject.scene)) Rewire(room);
    }
}
