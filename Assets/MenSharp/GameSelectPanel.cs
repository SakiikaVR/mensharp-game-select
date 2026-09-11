using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GameSelectPanel : MenSharpBehaviour
{
    [Header("Catalog — generated from GameSelect/Packages/*.json")]
    public string[] gameIds;
    public string[] titles;
    public string[] categories;
    public int[] minPlayers;
    public int[] maxPlayers;
    public Sprite[] icons;
    public GameObject[] gameRoots;
    public UdonBehaviour[] entryPoints;
    public string[] startEvents;
    public int selectedIndex = 1;

    [Header("View")]
    public Text leftTitle;
    public Text centerTitle;
    public Text rightTitle;
    public Text playerLabel;
    public Text clockLabel;
    private float clockElapsed = 0f;
    public Text detailLabel;
    public Text statusLabel;
    public Image leftIcon;
    public Image centerIcon;
    public Image rightIcon;
    public Image[] dots;
    public GameObject leftCard;
    public GameObject rightCard;
    public Button previousButton;
    public Button nextButton;
    public Button playButton;
    public RectTransform centerCard;
    public string selectedGameId;
    [Header("Pointer and keyboard focus")]
    public Button[] focusButtons;
    public GameObject[] focusFrames;
    public Button unfocusedTarget;
    public int focusedControl = -1;
    [Header("Carousel")]
    public Image[] carouselIcons;
    public float turnDuration = 0.36f;
    public bool isTurning;
    [Header("Shared room session (optional)")]
    public UdonBehaviour sessionController;
    public string sessionGameId = "";
    public GameObject sessionPlaceholder;
    public Text sessionTitle;
    private string displayedSessionId = "";
    private float turnTime;
    private int turnDirection;
    private int pendingTurns;

    public void Start() { RefreshView(); UpdateClock(); }
    public void UpdateClock()
    {
        if (clockLabel != null) clockLabel.text = System.DateTime.Now.ToString("HH:mm");
    }
    public void OnPlayerJoined(VRCPlayerApi player) { UpdatePlayers(); }
    public void OnPlayerLeft(VRCPlayerApi player) { UpdatePlayers(); }

    public void Previous()
    {
        QueueTurn(-1);
    }

    public void Next()
    {
        QueueTurn(1);
    }

    private void QueueTurn(int direction)
    {
        if (titles == null || titles.Length < 2) return;
        if (isTurning) { pendingTurns = Mathf.Clamp(pendingTurns + direction, -8, 8); return; }
        if (carouselIcons == null || carouselIcons.Length != 4)
        {
            selectedIndex = (selectedIndex + direction + titles.Length) % titles.Length;
            RefreshView(); FocusCenter(); return;
        }
        turnDirection = direction; turnTime = 0f; isTurning = true;
        leftIcon.enabled = false; centerIcon.enabled = false; rightIcon.enabled = false;
        statusLabel.text = "";
        for (int i = 0; i < 4; i++)
        {
            int slot = direction > 0 ? i - 1 : i - 2;
            int index = (selectedIndex + slot + titles.Length * 2) % titles.Length;
            carouselIcons[i].sprite = icons[index];
            carouselIcons[i].gameObject.SetActive(true);
        }
        DrawTurn(0f);
    }

    private void DrawTurn(float progress)
    {
        float eased = progress * progress * (3f - 2f * progress);
        for (int i = 0; i < 4; i++)
        {
            int startSlot = turnDirection > 0 ? i - 1 : i - 2;
            int endSlot = startSlot - turnDirection;
            float fromY = startSlot == 0 ? 139f : 95f;
            float toY = endSlot == 0 ? 139f : 95f;
            float fromSize = startSlot == 0 ? 335f : 267f;
            float toSize = endSlot == 0 ? 335f : 267f;
            RectTransform rect = carouselIcons[i].rectTransform;
            rect.anchoredPosition = new Vector2(Mathf.Lerp(startSlot * 463f, endSlot * 463f, eased), Mathf.Lerp(fromY, toY, eased));
            rect.sizeDelta = Vector2.one * Mathf.Lerp(fromSize, toSize, eased);
            float alpha = Mathf.Abs(startSlot) == 2 ? eased : Mathf.Abs(endSlot) == 2 ? 1f - eased : 1f;
            carouselIcons[i].color = new Color(1f, 1f, 1f, alpha);
        }
        float labelAlpha = Mathf.Abs(1f - progress * 2f);
        leftTitle.color = new Color(0.114f,0.114f,0.11f,labelAlpha);
        centerTitle.color = leftTitle.color; rightTitle.color = leftTitle.color;
        if (progress >= 0.5f)
        {
            int target = (selectedIndex + turnDirection + titles.Length) % titles.Length;
            leftTitle.text = titles[(target + titles.Length - 1) % titles.Length];
            centerTitle.text = titles[target];
            rightTitle.text = titles[(target + 1) % titles.Length];
        }
    }

    private void FinishTurn()
    {
        selectedIndex = (selectedIndex + turnDirection + titles.Length) % titles.Length;
        isTurning = false;
        for (int i = 0; i < carouselIcons.Length; i++) carouselIcons[i].gameObject.SetActive(false);
        leftIcon.enabled = true; centerIcon.enabled = true; rightIcon.enabled = true;
        leftTitle.color = new Color(0.114f,0.114f,0.11f,1f);
        centerTitle.color = leftTitle.color; rightTitle.color = leftTitle.color;
        RefreshView(); FocusCenter();
    }

    public void Update()
    {
        clockElapsed += Time.unscaledDeltaTime;
        if (clockElapsed >= 1f) { clockElapsed = 0f; UpdateClock(); }
        // Start queued turns on a fresh frame, outside the Select/OnSelect
        // callback chain (which re-enters this Udon program).
        if (!isTurning && pendingTurns != 0)
        {
            int direction = pendingTurns > 0 ? 1 : -1;
            pendingTurns -= direction;
            QueueTurn(direction);
        }
        if (focusedControl >= 0)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ReleaseFocus();
            else if (Input.GetKeyDown(KeyCode.Tab))
                CycleFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) FocusRowPrevious();
            else if (Input.GetKeyDown(KeyCode.RightArrow)) FocusRowNext();
            else if (Input.GetKeyDown(KeyCode.DownArrow)) FocusPlay();
            else if (Input.GetKeyDown(KeyCode.UpArrow)) FocusCenter();
        }
        if (isTurning)
        {
            turnTime += Time.deltaTime;
            float progress = Mathf.Clamp01(turnTime / Mathf.Max(0.1f, turnDuration));
            DrawTurn(progress);
            if (progress >= 1f) FinishTurn();
        }
    }

    public void FocusPrevious() { FocusControl(0); }
    public void FocusLeft() { FocusControl(1); }
    public void FocusCenter() { FocusControl(2); }
    public void FocusRight() { FocusControl(3); }
    public void FocusNext() { FocusControl(4); }
    public void FocusPlay() { FocusControl(5); }

    private void FocusControl(int index)
    {
        if (focusButtons == null || index >= focusButtons.Length) return;
        Button button = focusButtons[index];
        if (!button.gameObject.activeInHierarchy || !button.interactable) return;
        focusedControl = index;
        for (int i = 0; i < focusFrames.Length; i++) focusFrames[i].SetActive(i == index);
        button.Select();
    }

    public void FocusForward() { CycleFocus(1); }
    public void FocusBackward() { CycleFocus(-1); }
    public void FocusRowPrevious() { MoveRow(-1); }
    public void FocusRowNext() { MoveRow(1); }
    private void MoveRow(int direction)
    {
        if (focusButtons == null || focusButtons.Length < 6) return;
        int next = focusedControl == 5 ? 2 : focusedControl;
        for (int i = 0; i < 5; i++)
        {
            next = (next + direction + 5) % 5;
            if (focusButtons[next].gameObject.activeInHierarchy && focusButtons[next].interactable)
            { FocusControl(next); return; }
        }
    }
    private void CycleFocus(int direction)
    {
        if (focusButtons == null || focusButtons.Length == 0) return;
        int next = focusedControl;
        for (int i = 0; i < focusButtons.Length; i++)
        {
            next = (next + direction + focusButtons.Length) % focusButtons.Length;
            if (focusButtons[next].gameObject.activeInHierarchy && focusButtons[next].interactable)
            { FocusControl(next); return; }
        }
    }

    public void ReleaseFocus()
    {
        ClearFocusVisuals();
        if (unfocusedTarget != null) unfocusedTarget.Select();
    }

    public void ClearFocusVisuals()
    {
        focusedControl = -1;
        if (focusFrames == null) return;
        for (int i = 0; i < focusFrames.Length; i++) focusFrames[i].SetActive(false);
    }

    public void UpdatePlayers()
    {
        if (playerLabel != null)
            playerLabel.text = Mathf.Max(1, VRCPlayerApi.GetPlayerCount()).ToString() + "  PLAYERS";
    }

    public void RefreshView()
    {
        int count = titles == null ? 0 : titles.Length;
        previousButton.interactable = count > 1;
        nextButton.interactable = count > 1;
        playButton.interactable = count > 0;
        if (focusButtons != null && focusButtons.Length == 6) focusButtons[2].interactable = count > 0;
        leftCard.SetActive(count > 2);
        rightCard.SetActive(count > 1);
        statusLabel.text = "";
        if (count == 0)
        {
            selectedIndex = 0;
            selectedGameId = "";
            centerTitle.text = "ゲームがありません";
            centerIcon.enabled = false;
            detailLabel.text = "COMING SOON";
        }
        else
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
            selectedGameId = gameIds[selectedIndex];
            int previous = (selectedIndex + count - 1) % count;
            int next = (selectedIndex + 1) % count;
            leftTitle.text = titles[previous];
            centerTitle.text = titles[selectedIndex];
            rightTitle.text = titles[next];
            leftIcon.sprite = icons[previous];
            centerIcon.sprite = icons[selectedIndex];
            rightIcon.sprite = icons[next];
            centerIcon.enabled = true;
            detailLabel.text = categories[selectedIndex] + "   /   " + minPlayers[selectedIndex].ToString()
                + "–" + maxPlayers[selectedIndex].ToString() + " PLAYERS";
        }
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].gameObject.SetActive(i < count);
            dots[i].color = i == selectedIndex ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0.76f, 0.76f, 0.76f, 1f);
        }
        UpdatePlayers();
        if (focusedControl >= 0)
        {
            if (count == 0) ReleaseFocus();
            else FocusControl(2);
        }
    }

    public void PlaySelected()
    {
        if (isTurning) return;
        if (titles == null || titles.Length == 0) return;
        if (sessionController != null)
        {
            statusLabel.text = "ルームでゲームを開いています…";
            sessionController.SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestOpen", gameIds[selectedIndex]);
            return;
        }
        if (gameRoots[selectedIndex] == null || entryPoints[selectedIndex] == null)
        {
            statusLabel.text = titles[selectedIndex] + " は準備中です";
            return;
        }
        for (int i = 0; i < gameRoots.Length; i++)
            if (gameRoots[i] != null) gameRoots[i].SetActive(i == selectedIndex);
        ClearFocusVisuals();
        // A newly activated UdonBehaviour initializes at the next frame.
        entryPoints[selectedIndex].SendCustomEventDelayedFrames(startEvents[selectedIndex], 1);
        statusLabel.text = titles[selectedIndex] + " を開始しました";
    }

    public void CloseRoomGame()
    {
        if (sessionController != null)
            sessionController.SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestClose");
    }

    public void _ApplySession()
    {
        if (displayedSessionId == sessionGameId) return;
        displayedSessionId = sessionGameId;
        isTurning = false; pendingTurns = 0;
        for (int i = 0; i < carouselIcons.Length; i++) carouselIcons[i].gameObject.SetActive(false);
        leftIcon.enabled = true; centerIcon.enabled = true; rightIcon.enabled = true;
        leftTitle.color = new Color(0.11f,0.11f,0.11f,1f);
        centerTitle.color = leftTitle.color; rightTitle.color = leftTitle.color;
        ClearFocusVisuals();
        for (int i = 0; i < gameRoots.Length; i++)
            if (gameRoots[i] != null) gameRoots[i].SetActive(false);
        if (sessionPlaceholder != null) sessionPlaceholder.SetActive(false);
        if (sessionGameId == "") { RefreshView(); return; }
        int selected = -1;
        for (int i = 0; i < gameIds.Length; i++) if (gameIds[i] == sessionGameId) selected = i;
        if (selected < 0) return;
        selectedIndex = selected;
        RefreshView();
        if (gameRoots[selected] == null || entryPoints[selected] == null)
        {
            if (sessionPlaceholder != null)
            {
                sessionTitle.text = titles[selected] + "\n\nゲーム本体は準備中です";
                sessionPlaceholder.SetActive(true);
            }
            return;
        }
        gameRoots[selected].SetActive(true);
        entryPoints[selected].SendCustomEventDelayedFrames(startEvents[selected], 1);
    }
}
