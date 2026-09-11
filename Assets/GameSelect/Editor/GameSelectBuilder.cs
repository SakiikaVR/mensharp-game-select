using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using Object = UnityEngine.Object;

public static class GameSelectBuilder
{
    public const string Root = "Assets/GameSelect";
    public const string PrefabPath = Root + "/GameSelectPanel.prefab";
    private static readonly Color Ink = new Color32(29, 29, 28, 255);
    private static Font japanese, latin;

    [Serializable]
    public class Package
    {
        public int schemaVersion;
        public string id, title, category, icon, iconPath, prefab, startEvent;
        public int order, minPlayers, maxPlayers;
        public string gameType;
        public Newtonsoft.Json.Linq.JObject clicker;
        public string builderType;
        [JsonIgnore] public string packageDirectory;
    }

    public static Package[] ReadCatalog()
    {
        var result = new List<Package>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Directory.CreateDirectory(Root + "/Packages");
        foreach (string file in Directory.GetFiles(Root + "/Packages", "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".gamepackage", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            Package p;
            try { p = JsonConvert.DeserializeObject<Package>(File.ReadAllText(file)); }
            catch (Exception e) { throw new InvalidDataException(file + ": invalid JSON: " + e.Message); }
            if (p == null || p.schemaVersion != 1) throw new InvalidDataException(file + ": schemaVersion must be 1.");
            p.packageDirectory = Path.GetDirectoryName(file).Replace('\\','/');
            p.iconPath = ResolvePackageAsset(p, p.iconPath);
            p.prefab = ResolvePackageAsset(p, p.prefab);
            if (string.IsNullOrWhiteSpace(p.id) || !Regex.IsMatch(p.id, "^[a-z0-9][a-z0-9-]*$") || !ids.Add(p.id))
                throw new InvalidDataException(file + ": id must be unique, lowercase letters/digits/hyphens.");
            if (string.IsNullOrWhiteSpace(p.title) || p.title.Length > 24) throw new InvalidDataException(file + ": title must contain 1–24 characters.");
            if (p.minPlayers < 1 || p.maxPlayers < p.minPlayers || p.maxPlayers > 80) throw new InvalidDataException(file + ": invalid player range (1–80).");
            p.category = string.IsNullOrWhiteSpace(p.category) ? "GAME" : p.category;
            if (p.category.Length > 32) throw new InvalidDataException(file + ": category is too long (32 max).");
            p.startEvent = string.IsNullOrWhiteSpace(p.startEvent) ? "StartGame" : p.startEvent;
            if (!Regex.IsMatch(p.startEvent, "^[A-Za-z][A-Za-z0-9_]*$")) throw new InvalidDataException(file + ": startEvent must be a public event name.");
            if (!string.IsNullOrEmpty(p.iconPath) && AssetDatabase.LoadAssetAtPath<Sprite>(p.iconPath) == null)
                throw new InvalidDataException(file + ": iconPath must reference an imported Sprite.");
            if (string.IsNullOrEmpty(p.iconPath) && !string.IsNullOrEmpty(p.icon) && !new[] { "cookie", "cards", "shrine", "gamepad" }.Contains(p.icon))
                throw new InvalidDataException(file + ": unknown icon key.");
            if (!string.IsNullOrEmpty(p.prefab))
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab);
                if (asset == null || !PrefabUtility.IsPartOfPrefabAsset(asset)) throw new InvalidDataException(file + ": prefab path does not reference a prefab.");
                if (asset.GetComponents<UdonBehaviour>().Length != 1) throw new InvalidDataException(file + ": game prefab root must have exactly one UdonBehaviour (MenSharp backing behaviour is supported).");
            }
            if (!string.IsNullOrEmpty(p.gameType))
            {
                if (!string.IsNullOrEmpty(p.prefab)) throw new InvalidDataException(file + ": specify either gameType or prefab.");
                PackageBuilderType(p).GetMethod("ValidatePackage").Invoke(null, new object[] { p });
                p.startEvent = "StartGame";
            }
            result.Add(p);
        }
        if (result.Count > 64) throw new InvalidDataException("Game Select supports at most 64 packages.");
        return result.OrderBy(p => p.order).ThenBy(p => p.id, StringComparer.Ordinal).ToArray();
    }

    public static string ResolvePackageAsset(Package p, string path)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith("Assets/", StringComparison.Ordinal)) return path;
        string root = Path.GetFullPath(p.packageDirectory) + Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(Path.Combine(p.packageDirectory, path));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset path escapes package folder: " + path);
        return (p.packageDirectory + "/" + path).Replace('\\','/');
    }

    private static Type PackageBuilderType(Package p)
    {
        if (string.IsNullOrEmpty(p.builderType)) throw new InvalidDataException("Package requires builderType: " + p.id);
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(p.builderType)).FirstOrDefault(t => t != null);
        if (type == null || type.GetMethod("Create") == null || type.GetMethod("ValidatePackage") == null)
            throw new InvalidDataException("Package builder is missing or not compiled: " + p.builderType);
        return type;
    }

    [MenuItem("Game Select/Create Panel in Scene")]
    public static void CreatePanel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        ConfigureArt();
        Package[] packages = ReadCatalog(); // Validate before modifying the scene.
        if (Object.FindObjectOfType<GameSelectPanel>() != null) throw new InvalidOperationException("A Game Select panel already exists. Use Refresh JSON Catalog.");
        MenSharpCompiler.CompileAll();
        japanese = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansJP-Bold.otf");
        latin = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansJP-Bold.otf");
        if (japanese == null || latin == null) throw new InvalidOperationException("Panel fonts were not imported.");
        var root = new GameObject("GameSelectPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Create Game Select Panel");
        root.layer = 8; // Interactive: VRChat ignores layer 5 (UI) while its menu is closed.
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920, 1080);
        rect.position = new Vector3(0, 2.1f, 4);
        rect.localScale = Vector3.one * 0.0025f;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1;
        root.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var panel = root.AddComponent<GameSelectPanel>();

        Img("Background", rect, 0, 0, 1920, 1080, "background", Color.white);
        Label("Heading", rect, "G A M E   S E L E C T", -570, 462, 630, 60, 34, false, TextAnchor.MiddleLeft);
        Img("HeaderRule", rect, 22, 460, 1030, 1.5f, null, new Color32(193,193,190,255));
        Img("PlayersIcon", rect, 620, 462, 56, 56, "players", Color.white);
        panel.playerLabel = Label("Players", rect, "1  PLAYERS", 792, 461, 250, 55, 28, false, TextAnchor.MiddleLeft);
        Img("OnlineDot", rect, 887, 460, 20, 20, "circle", Ink);

        RectTransform left = Card(rect, "PreviousCard", -463, 66, 388, 404, false);
        RectTransform center = Card(rect, "SelectedCard", 0, 96, 474, 522, true);
        RectTransform right = Card(rect, "NextCard", 463, 66, 388, 404, false);
        panel.leftCard = left.gameObject; panel.rightCard = right.gameObject; panel.centerCard = center;
        panel.leftIcon = Img("Icon", left, 0, 29, 267, 267, "cookie", Color.white);
        panel.centerIcon = Img("Icon", center, 0, 43, 335, 335, "cards", Color.white);
        panel.rightIcon = Img("Icon", right, 0, 29, 267, 267, "shrine", Color.white);
        panel.leftTitle = Label("Title", left, "", 0, -139, 354, 70, 41, true);
        panel.centerTitle = Label("Title", center, "", 0, -167, 430, 93, 62, true);
        panel.rightTitle = Label("Title", right, "", 0, -139, 354, 70, 41, true);
        panel.detailLabel = Label("Details", rect, "", 0, -206, 830, 40, 19, false);
        panel.detailLabel.color = new Color32(126,126,123,255);

        var prev = Img("Previous", rect, -857, 65, 110, 110, "circle", new Color32(231,231,229,255));
        Img("Chevron", prev.rectTransform, 0, 0, 60, 60, "chevron", Color.white);
        var next = Img("Next", rect, 857, 65, 110, 110, "circle", new Color32(231,231,229,255));
        Img("Chevron", next.rectTransform, 0, 0, 60, 60, "chevron", Color.white).rectTransform.localEulerAngles = new Vector3(0,0,180);
        panel.previousButton = Button(prev);
        panel.nextButton = Button(next);
        Shadow(rect, "PlayShadow", 0, -305, 652, 164);
        var play = Img("Play", rect, 0, -302, 628, 138, "round", Ink);
        play.pixelsPerUnitMultiplier = 0.44f;
        panel.playButton = Button(play);
        Img("PlayIcon", play.rectTransform, -129, 0, 80, 80, "play", Color.white);
        Label("Label", play.rectTransform, "P L A Y", 55, 0, 335, 100, 61, false).color = Color.white;
        panel.statusLabel = Label("Status", rect, "", 0, -413, 1220, 46, 23, true);
        panel.statusLabel.color = new Color32(108,108,105,255);
        Rect("Pagination", rect, 0, -469, 1300, 34);
        Rect("PackageContent", rect, 0, 0, 1920, 1080);
        ApplyCatalog(panel, packages);
        MenSharpProxy.SyncThenTransfer(new List<GameObject> { root }, false);
        var backing = root.GetComponents<UdonBehaviour>().FirstOrDefault(u => u.programSource != null);
        if (backing == null) throw new InvalidOperationException("MenSharp program did not compile/pair.");
        UnityEventTools.AddStringPersistentListener(panel.previousButton.onClick, backing.SendCustomEvent, "Previous");
        UnityEventTools.AddStringPersistentListener(panel.nextButton.onClick, backing.SendCustomEvent, "Next");
        UnityEventTools.AddStringPersistentListener(panel.playButton.onClick, backing.SendCustomEvent, "PlaySelected");
        UnityEventTools.AddStringPersistentListener(Button(left.GetComponent<Image>()).onClick, backing.SendCustomEvent, "Previous");
        UnityEventTools.AddStringPersistentListener(Button(right.GetComponent<Image>()).onClick, backing.SendCustomEvent, "Next");
        UnityEventTools.AddStringPersistentListener(Button(center.GetComponent<Image>()).onClick, backing.SendCustomEvent, "PlaySelected");
        GameSelectFocusSetup.Configure(panel);
        MenSharpProxy.SyncThenTransfer(new List<GameObject> { root }, false);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.Frame(new Bounds(rect.position, new Vector3(5,3,0.2f)), false);
        Debug.Log("Game Select: created 16:9 MenSharp panel, " + packages.Length + " JSON packages.");
    }

    [MenuItem("Game Select/Refresh JSON Catalog")]
    public static void RefreshCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Package[] packages = ReadCatalog();
        // Validate all files first: an invalid edit preserves the last usable catalog.
        if (File.Exists(PrefabPath))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyCatalog(root.GetComponent<GameSelectPanel>(), packages);
                GameSelectFocusSetup.Configure(root.GetComponent<GameSelectPanel>());
                MenSharpProxy.SyncThenTransfer(new List<GameObject> { root }, false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (GameSelectPanel panel in Object.FindObjectsOfType<GameSelectPanel>(true))
        {
            if (!panel.gameObject.scene.IsValid() || EditorSceneManager.IsPreviewScene(panel.gameObject.scene)) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(panel) &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject) == PrefabPath)
            {
                // Unity already propagates the updated prefab hierarchy and its
                // references. Rebuilding inherited children here is not allowed.
                panel.RefreshView();
            }
            else ApplyCatalog(panel, packages);
            MenSharpProxy.SyncThenTransfer(new List<GameObject> { panel.gameObject }, false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Game Select: refreshed " + packages.Length + " JSON packages.");
    }

    private static void ApplyCatalog(GameSelectPanel panel, Package[] packages)
    {
        string keepId = panel.selectedGameId;
        int n = packages.Length;
        panel.gameIds = new string[n]; panel.titles = new string[n]; panel.categories = new string[n];
        panel.minPlayers = new int[n]; panel.maxPlayers = new int[n]; panel.icons = new Sprite[n];
        panel.gameRoots = new GameObject[n]; panel.entryPoints = new UdonBehaviour[n]; panel.startEvents = new string[n];
        Transform content = panel.transform.Find("PackageContent");
        // Only this generated container is rebuilt; external scene objects are never touched.
        for (int i = content.childCount - 1; i >= 0; i--) Object.DestroyImmediate(content.GetChild(i).gameObject);
        for (int i = 0; i < n; i++)
        {
            Package p = packages[i];
            panel.gameIds[i] = p.id; panel.titles[i] = p.title; panel.categories[i] = p.category;
            panel.minPlayers[i] = p.minPlayers; panel.maxPlayers[i] = p.maxPlayers; panel.startEvents[i] = p.startEvent;
            panel.icons[i] = string.IsNullOrEmpty(p.iconPath) ? Sprite(string.IsNullOrEmpty(p.icon) ? "gamepad" : p.icon) : AssetDatabase.LoadAssetAtPath<Sprite>(p.iconPath);
            if (p.id == keepId) panel.selectedIndex = i;
            if (!string.IsNullOrEmpty(p.prefab))
            {
                GameObject game = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab), content);
                game.name = p.id;
                panel.gameRoots[i] = game; panel.entryPoints[i] = game.GetComponent<UdonBehaviour>();
                game.SetActive(false);
            }
            else if (!string.IsNullOrEmpty(p.gameType))
            {
                var game = (GameObject)PackageBuilderType(p).GetMethod("Create").Invoke(null, new object[] { p, content });
                panel.gameRoots[i] = game; panel.entryPoints[i] = game.GetComponent<UdonBehaviour>();
            }
        }
        Transform pagination = panel.transform.Find("Pagination");
        for (int i = pagination.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pagination.GetChild(i).gameObject);
        panel.dots = new Image[n];
        float gap = Mathf.Min(37, 1200f / Mathf.Max(1,n));
        for (int i = 0; i < n; i++) panel.dots[i] = Img("Dot" + i, pagination, (i-(n-1)*0.5f)*gap, 0, 18, 18, "circle", Ink);
        panel.RefreshView();
        EditorUtility.SetDirty(panel);
    }

    private static void ConfigureArt()
    {
        foreach (string path in Directory.GetFiles(Root + "/Art", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100;
            string key = Path.GetFileNameWithoutExtension(path);
            importer.spriteBorder = key == "round" || key == "selected" ? new Vector4(34,34,34,34) : key == "shadow" ? new Vector4(70,70,70,70) : key == "focus-round" ? new Vector4(40,40,40,40) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/" + name + ".png");
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 8;
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f,0.5f);
        r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h);
        return r;
    }
    private static Image Img(string name, Transform parent, float x, float y, float w, float h, string sprite, Color color)
    {
        var image = Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();
        image.sprite = sprite == null ? null : Sprite(sprite); image.color = color; image.raycastTarget = false;
        if (sprite == "round" || sprite == "selected" || sprite == "shadow") image.type = Image.Type.Sliced;
        return image;
    }
    private static void Shadow(Transform parent, string name, float x, float y, float w, float h) => Img(name,parent,x,y-12,w+70,h+70,"shadow",Color.white);
    private static RectTransform Card(Transform parent, string name, float x, float y, float w, float h, bool selected)
    {
        Shadow(parent,name+"Shadow",x,y,w,h);
        var image = Img(name,parent,x,y,w,h,selected ? "selected" : "round",Color.white);
        image.pixelsPerUnitMultiplier = selected ? 0.63f : 1;
        return image.rectTransform;
    }
    private static Text Label(string name, Transform parent, string text, float x, float y, float w, float h, int size, bool jp, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var label = Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();
        label.font = jp ? japanese : latin; label.fontSize = size; label.text = text; label.color = Ink;
        label.alignment = anchor; label.raycastTarget = false; label.supportRichText = false;
        label.resizeTextForBestFit = true; label.resizeTextMinSize = Math.Min(21,size); label.resizeTextMaxSize = size;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }
    private static Button Button(Image image)
    {
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(0.92f,0.92f,0.91f); colors.pressedColor = new Color(0.78f,0.78f,0.77f); colors.fadeDuration = 0.12f; button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        return button;
    }

    [MenuItem("Game Select/Capture Panel Preview")]
    public static void CapturePreview()
    {
        GameSelectPanel panel = Object.FindObjectOfType<GameSelectPanel>();
        if (panel == null) throw new InvalidOperationException("No GameSelectPanel in the scene.");
        Canvas.ForceUpdateCanvases();
        var go = new GameObject("GameSelectCaptureCamera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 540 * panel.transform.lossyScale.y;
        camera.transform.SetPositionAndRotation(panel.transform.position - panel.transform.forward * 2, panel.transform.rotation);
        camera.nearClipPlane = 0.01f; camera.farClipPlane = 5; camera.cullingMask = 1 << panel.gameObject.layer;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
        var rt = new RenderTexture(1920,1080,24); var before = RenderTexture.active;
        var texture = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply(); Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/game-select-preview.png",texture.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=before; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); Object.DestroyImmediate(go); }
    }
}

[InitializeOnLoad]
public class GameSelectCatalogImporter : AssetPostprocessor
{
    private static bool queued;
    static GameSelectCatalogImporter()
    {
        // Recover pending imports across script/domain reloads, including edits
        // made while playing. Apply only after the editor is ready again.
        if (File.Exists(GameSelectBuilder.PrefabPath)) QueueRefresh();
    }
    private static void QueueRefresh()
    {
        if (queued) return;
        queued = true;
        EditorApplication.update += RefreshWhenReady;
    }
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (queued) return;
        if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => p.StartsWith(GameSelectBuilder.Root + "/Packages/",StringComparison.Ordinal) && (p.EndsWith(".json",StringComparison.OrdinalIgnoreCase) || p.EndsWith(".gamepackage",StringComparison.OrdinalIgnoreCase)))) return;
        QueueRefresh();
    }
    private static void RefreshWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= RefreshWhenReady;
        queued = false;
        if (!File.Exists(GameSelectBuilder.PrefabPath)) return;
        try { GameSelectBuilder.RefreshCatalog(); }
        catch (Exception e) { Debug.LogError("Game Select: catalog unchanged. " + e.Message); }
    }
}

