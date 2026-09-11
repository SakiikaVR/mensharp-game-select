using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.Udon;

// Idempotent wiring: upgrades both existing panels and newly generated prefabs.
public static class GameSelectFocusSetup
{
    public static void Configure(GameSelectPanel panel)
    {
        // World UI must remain raycastable while the VRChat / ClientSim menu is closed.
        // Migrate only the panel's UI; game package content retains its own layers.
        panel.gameObject.layer = 8;
        foreach (var canvasRect in panel.GetComponentsInChildren<RectTransform>(true))
        {
            var content = panel.transform.Find("PackageContent");
            if (content != null && canvasRect.IsChildOf(content)) continue;
            canvasRect.gameObject.layer = 8;
        }
        var udon = panel.GetComponent<UdonBehaviour>();
        if (udon == null) return;
        string[] paths = { "Previous", "PreviousCard", "SelectedCard", "NextCard", "Next", "Play" };
        string[] events = { "FocusPrevious", "FocusLeft", "FocusCenter", "FocusRight", "FocusNext", "FocusPlay" };
        panel.focusButtons = new Button[6];
        panel.focusFrames = new GameObject[6];
        for (int i = 0; i < 6; i++)
        {
            var target = panel.transform.Find(paths[i]);
            var button = target.GetComponent<Button>();
            if (button == null) continue; // Initial builder wires buttons before the next catalog refresh.
            panel.focusButtons[i] = button;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            var trigger = target.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerEnter || e.eventID == EventTriggerType.Select || e.eventID == EventTriggerType.Deselect);
            Add(trigger, EventTriggerType.PointerEnter, udon, events[i]);
            Add(trigger, EventTriggerType.Select, udon, events[i]);
            Add(trigger, EventTriggerType.Deselect, udon, "ClearFocusVisuals");
            Transform existing = target.Find("FocusFrame");
            GameObject frame = existing == null ? new GameObject("FocusFrame",typeof(RectTransform),typeof(Image)) : existing.gameObject;
            frame.layer = target.gameObject.layer;
            var rect = frame.GetComponent<RectTransform>(); rect.SetParent(target,false);
            rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(-11,-11); rect.offsetMax=new Vector2(11,11);
            var image=frame.GetComponent<Image>(); image.raycastTarget=false;
            bool circular=i==0 || i==4;
            image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameSelect/Art/"+(circular?"focus-circle":"focus-round")+".png");
            image.type=circular?Image.Type.Simple:Image.Type.Sliced;
            image.color=new Color32(58,58,56,255);
            image.pixelsPerUnitMultiplier = i==5 ? 0.44f : i==2 ? 0.63f : 1f;
            frame.SetActive(false); panel.focusFrames[i]=frame;
        }
        var release=panel.transform.Find("UnfocusedTarget");
        if(release==null)
        {
            var go=new GameObject("UnfocusedTarget",typeof(RectTransform),typeof(Button));
            go.transform.SetParent(panel.transform,false); go.GetComponent<RectTransform>().sizeDelta=Vector2.zero;
            release=go.transform;
        }
        panel.unfocusedTarget=release.GetComponent<Button>();
        var disabledNav=panel.unfocusedTarget.navigation;disabledNav.mode=Navigation.Mode.None;panel.unfocusedTarget.navigation=disabledNav;
        panel.focusedControl=-1;
        Transform carousel = panel.transform.Find("CarouselIcons");
        if (carousel == null)
        {
            var layer = new GameObject("CarouselIcons", typeof(RectTransform));
            layer.transform.SetParent(panel.transform, false);
            carousel = layer.transform;
        }
        var layerRect = carousel.GetComponent<RectTransform>();
        layerRect.anchorMin = layerRect.anchorMax = layerRect.pivot = new Vector2(0.5f,0.5f);
        layerRect.anchoredPosition = Vector2.zero; layerRect.sizeDelta = new Vector2(1410,800);
        // Keep incoming/outgoing icons inside the card strip, clear of arrow buttons.
        if (carousel.GetComponent<RectMask2D>() == null) carousel.gameObject.AddComponent<RectMask2D>();
        carousel.SetAsLastSibling();
        panel.carouselIcons = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Transform item = carousel.Find("Icon" + i);
            if (item == null)
            {
                var go = new GameObject("Icon" + i,typeof(RectTransform),typeof(Image));
                go.transform.SetParent(carousel,false); item=go.transform;
            }
            item.gameObject.layer=panel.gameObject.layer;
            var r=item.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0.5f,0.5f);
            panel.carouselIcons[i]=item.GetComponent<Image>();panel.carouselIcons[i].raycastTarget=false;
            item.gameObject.SetActive(false);
        }
        panel.isTurning=false;
        EditorUtility.SetDirty(panel);
    }
    private static void Add(EventTrigger trigger,EventTriggerType type,UdonBehaviour udon,string method)
    {
        var entry=new EventTrigger.Entry {eventID=type};
        UnityEventTools.AddStringPersistentListener(entry.callback,udon.SendCustomEvent,method);
        trigger.triggers.Add(entry);
    }
    [MenuItem("Game Select/Upgrade Focus Controls")]
    public static void Upgrade()
    {
        foreach(string name in new[]{"focus-round","focus-circle"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/GameSelect/Art/"+name+".png");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.spriteBorder=name=="focus-round"?new Vector4(40,40,40,40):Vector4.zero;
            importer.SaveAndReimport();
        }
        GameSelectBuilder.RefreshCatalog();
    }
}
