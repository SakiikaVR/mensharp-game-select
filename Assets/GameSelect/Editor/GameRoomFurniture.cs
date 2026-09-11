using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class GameRoomFurniture
{
    private const string Art="Assets/GameSelect/RoomArt/";
    [MenuItem("Game Select/Build Table Room")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var prefab=PrefabUtility.LoadPrefabContents(GameRoomSetup.RoomPrefabPath);
        try {Configure(prefab.GetComponent<GameRoomSession>());PrefabUtility.SaveAsPrefabAsset(prefab,GameRoomSetup.RoomPrefabPath);}
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        foreach(var room in Object.FindObjectsOfType<GameRoomSession>())Configure(room);
        // Keep an existing world spawn from starting inside the new table.
        var descriptor=Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>();
        if(descriptor!=null&&descriptor.spawns!=null)
            foreach(var spawn in descriptor.spawns)
                if(spawn!=null&&Mathf.Abs(spawn.position.x)<2.9f&&Mathf.Abs(spawn.position.z)<2.9f)
                {spawn.position=new Vector3(0,.12f,-4.6f);spawn.rotation=Quaternion.identity;EditorUtility.SetDirty(spawn);}
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
    }
    public static void Configure(GameRoomSession room)
    {
        var panels=room.GetComponentsInChildren<GameSelectPanel>(true);
        var wood=Material("Walnut",new Color(.9f,.86f,.8f),"wood.png");
        var carpet=Material("Carpet",Color.white,"carpet.png");
        var metal=Material("Graphite",new Color(.055f,.065f,.06f),null);
        var group=room.transform.Find("Furniture");
        if(group==null){var go=new GameObject("Furniture");group=go.transform;group.SetParent(room.transform,false);}
        Cube(group,"SquareCarpet",new Vector3(0,.016f,0),new Vector3(9.6f,.025f,9.6f),carpet,false);
        Cube(group,"SquareTable",new Vector3(0,1.02f,0),new Vector3(4.9f,.18f,4.9f),wood,true);
        for(int x=0;x<2;x++)for(int z=0;z<2;z++)Cube(group,"Leg"+x+z,new Vector3(x==0?-2.05f:2.05f,.49f,z==0?-2.05f:2.05f),new Vector3(.18f,.94f,.18f),metal,true);
        for(int i=0;i<panels.Length&&i<4;i++)
        {
            float yaw=180+i*90;
            Vector3 outward=Quaternion.Euler(0,i*90,0)*Vector3.forward;
            var panel=panels[i].transform;panel.localPosition=outward*2.48f+Vector3.up*2.03f;panel.localRotation=Quaternion.Euler(0,yaw,0);panel.localScale=Vector3.one*.001875f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            var frame=Cube(group,"MonitorFrame"+i,panel.localPosition-outward*.07f,new Vector3(3.72f,2.15f,.12f),metal,true);
            frame.localRotation=Quaternion.Euler(0,yaw,0);PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
        }
        EditorUtility.SetDirty(room.gameObject);
    }
    private static Material Material(string name,Color color,string texture)
    {
        string path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Glossiness",.13f);if(texture!=null)m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+texture);EditorUtility.SetDirty(m);return m;
    }
    private static Transform Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collider)
    {
        var t=parent.Find(name);
        if(t==null){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;t=go.transform;t.SetParent(parent,false);}
        t.localPosition=position;t.localScale=scale;t.GetComponent<Renderer>().sharedMaterial=material;
        t.GetComponent<Collider>().enabled=collider;
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<Renderer>());
        return t;
    }
}
