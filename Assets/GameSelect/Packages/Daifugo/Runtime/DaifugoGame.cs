using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DaifugoGame : MenSharpBehaviour
{
    public UdonBehaviour self, table, sessionController;
    public UdonBehaviour[] roomViews;
    public Text statusLabel, tableLabel, playersLabel, handLabel, hintLabel, settingsLabel;
    public Text sharedPileLabel, sharedStateLabel, sharedTurnLabel;
    public Text[] cardLabels, ruleLabels, opponentNames, opponentCounts;
    public GameObject[] opponentTiles;
    public Button[] cardButtons, ruleButtons, bombButtons;
    public Button joinButton, leaveButton, startButton, playButton, passButton, addCpuButton;
    public GameObject settingsPanel, bombPanel, bookPanel;
    public Text bookLabel;
    public string[] bookPages;
    private int bookPage = 0;
    private float cpuElapsed = 0f;
    public AudioSource effectsSource, voiceSource;
    public AudioClip turnSound, cardSound, passSound, winSound;
    public AudioClip[] ruleVoices;
    public string[] ruleNames;
    public bool voiceEnabled = false;
    public bool soundEnabled = true;

    // Only the first room view is the authority. Other views read its snapshot.
    [UdonSynced] public bool cpuEnabled = true;
    [UdonSynced] public bool[] disqualified = new bool[4];
    [UdonSynced] public int penaltyCount = 0;
    [UdonSynced] public int revision = 0;
    [UdonSynced] public int phase = 0; // lobby, playing, results, exchange
    [UdonSynced] public int rules = 4095;
    [UdonSynced] public int[] seats = new int[4];
    [UdonSynced] public string[] names = new string[] { "", "", "", "" };
    [UdonSynced] public int[] owners = new int[53]; // 0..51 = suit*13 + (3..2), 52 joker
    [UdonSynced] public int[] places = new int[4];
    [UdonSynced] public bool[] passed = new bool[4];
    [UdonSynced] public int turn = -1;
    [UdonSynced] public int lastSeat = -1;
    [UdonSynced] public int pileCount = 0, pileRank = -1, pileSuit = 0, lockSuit = 0;
    [UdonSynced] public bool pileStraight = false, pileJoker = false;
    [UdonSynced] public bool revolution = false, jackBack = false;
    [UdonSynced] public string pileText = "", message = "参加を押してください（1～4人）";
    [UdonSynced] public int finished = 0;
    [UdonSynced] public int pending = 0, pendingCount = 0, pendingSeat = -1, receiver = -1;
    [UdonSynced] public int giveRemaining = 0, discardRemaining = 0, bombRemaining = 0;
    [UdonSynced] public int skipCount = 0;
    [UdonSynced] public bool clearAfter = false;
    [UdonSynced] public int effectMask = 0, soundEvent = 0;
    [UdonSynced] public int[] previousPlaces = new int[4];
    [UdonSynced] public int exchangeStep = 0, roundLead = -1;
    private bool[] selected = new bool[53];
    private int seenRevision = -1, seenTurn = -1, queuedVoices = 0;

    public void Start() { StartGame(); }
    public void StartGame()
    {
        seenRevision = -1; queuedVoices = 0;
        if (voiceSource != null) voiceSource.Stop();
        if (self != null && table == null) table = self;
        if (table == self && Networking.IsOwner(gameObject))
        {
            for (int s = 0; s < 4; s++) if (seats[s] > 0 && VRCPlayerApi.GetPlayerById(seats[s]) == null) RemoveSeat(s);
            Commit();
        }
        _Render();
    }
    public void OnDeserialization() { if (table == self) NotifyViews(); }
    public void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (table != self) return;
        if (Networking.IsOwner(gameObject))
        {
            for (int s = 0; s < 4; s++)
                if (seats[s] > 0 && VRCPlayerApi.GetPlayerById(seats[s]) == null) RemoveSeat(s);
            Commit();
        }
    }
    public void OnPlayerJoined(VRCPlayerApi player) { if (table == self && Networking.IsOwner(gameObject)) RequestSerialization(); }
    public void OnPlayerLeft(VRCPlayerApi player)
    {
        if (table != self || !Networking.IsOwner(gameObject)) return;
        int s = SeatOf(player.playerId);
        if (s >= 0) { RemoveSeat(s); Commit(); }
    }
    private int SeatOf(int id) { for (int s = 0; s < 4; s++) if (id > 0 && seats[s] == id) return s; return -1; }
    private int FirstSeat() { for (int s = 0; s < 4; s++) if (seats[s] > 0) return s; return -1; }
    private int PlayerCount() { int n = 0; for (int s = 0; s < 4; s++) if (seats[s] != 0) n++; return n; }
    public int HandCount(int s) { int n = 0; for (int c = 0; c < 53; c++) if (owners[c] == s) n++; return n; }
    private bool Rule(int bit) { return (rules & (1 << bit)) != 0; }
    private bool Active(int s) { return s >= 0 && seats[s] != 0 && places[s] == 0 && HandCount(s) > 0; }
    private int NextActive(int s)
    {
        for (int i = 1; i <= 4; i++) { int n = (s + i + 4) % 4; if (Active(n)) return n; }
        return -1;
    }
    private void Commit() { revision++; RequestSerialization(); NotifyViews(); }
    private void NotifyViews()
    {
        if (roomViews == null || roomViews.Length == 0) { _Render(); return; }
        for (int i = 0; i < roomViews.Length; i++) if (roomViews[i] != null) roomViews[i].SendCustomEvent("_Render");
    }
    [NetworkCallable(maxEventsPerSecond: 6)]
    public void RequestAction(int action, int a, int b, int expected)
    {
        if (table != self || !Networking.IsOwner(gameObject)) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null) return;
        int seat = SeatOf(caller.playerId);
        effectMask = 0; soundEvent = 0;
        if (action == 0)
        {
            if (phase != 0 || seat >= 0) return;
            for (int s = 0; s < 4; s++) if (seats[s] <= 0) { seats[s] = caller.playerId; names[s] = caller.displayName; Commit(); return; }
            return;
        }
        if (seat < 0 || expected != revision) return;
        if (action == 1) { RemoveSeat(seat); Commit(); return; }
        if (action == 2 && seat == FirstSeat() && (phase == 0 || phase == 2) ) { if (cpuEnabled) { for (int k = 0; k < 4; k++) AddCpu(); }
            if (PlayerCount() < 2) { message = "CPUがOFFです。2人以上で参加してください"; Commit(); return; }
            Deal(); Commit(); return; }
        if (action == 8 && seat == FirstSeat() && phase == 0)
        {
            cpuEnabled = !cpuEnabled;
            if (!cpuEnabled) for (int k = 0; k < 4; k++) if (seats[k] < 0) { seats[k] = 0; places[k] = 0; previousPlaces[k] = 0; }
            Commit(); return;
        }
        if (action == 5 && seat == FirstSeat() && phase == 0 && a >= 0 && a < 12) { rules ^= 1 << a; Commit(); return; }
        if (action == 7 && seat == FirstSeat() && phase == 2) { phase = 0; for (int s = 0; s < 4; s++) { places[s] = 0; previousPlaces[s] = 0; } message = "参加とルールを設定してください"; Commit(); return; }
        bool changed = false;
        if (action == 3) changed = TryPlay(seat, a, b);
        if (action == 4) changed = TryPass(seat);
        if (action == 6) changed = TryBomb(seat, a);
        if (changed) Commit();
    }
    public void AddCpu()
    {
        for (int s = 0; s < 4; s++) if (seats[s] == 0) { seats[s] = -s - 1; names[s] = "CPU " + (s + 1).ToString(); return; }
    }
    public bool CpuStep()
    {
        if ((phase != 1 && phase != 3) || turn < 0) return false;
        int seat = turn;
        if (pending == 3)
        {
            int best = 0, bestScore = -999;
            for (int r = 0; r < 13; r++)
            {
                int score = Random.Range(0, 3);
                for (int s = 0; s < 4; s++) if (owners[s * 13 + r] == seat) score -= 4;
                if (score > bestScore) { bestScore = score; best = r; }
            }
            return TryBomb(seat, best);
        }
        if (pending != 0)
        {
            int lo = 0, hi = 0, count = 0;
            for (int k = 0; k < 53 && count < pendingCount; k++)
            {
                int c = k == 52 ? 52 : (k % 4) * 13 + k / 4;
                if (owners[c] != seat) continue;
                if (c < 27) lo |= 1 << c; else hi |= 1 << (c - 27);
                count++;
            }
            return TryPlay(seat, lo, hi);
        }
        // Prefer shedding a longer legal combination, using only this seat's hand.
        if (Rule(6) && (pileCount == 0 || pileStraight))
        {
            for (int length = 13; length >= 3; length--)
            {
                if (pileCount > 0 && length != pileCount) continue;
                for (int suit = 0; suit < 4; suit++) for (int start = 0; start <= 13 - length; start++)
                {
                    int lo = 0, hi = 0, missing = 0;
                    for (int r = start; r < start + length; r++)
                    {
                        int c = suit * 13 + r;
                        if (owners[c] != seat) { missing++; continue; }
                        if (c < 27) lo |= 1 << c; else hi |= 1 << (c - 27);
                    }
                    if (missing == 1 && owners[52] == seat) hi |= 1 << 25;
                    else if (missing != 0) continue;
                    if (TryPlay(seat, lo, hi)) return true;
                }
            }
        }
        for (int count = 5; count >= 1; count--)
        {
            if (pileCount > 0 && count != pileCount) continue;
            for (int k = 0; k < 13; k++)
            {
                int rank = revolution != jackBack ? 12 - k : k;
                for (int mask = 1; mask < 16; mask++)
                {
                    int lo = 0, hi = 0, n = 0; bool valid = true;
                    for (int suit = 0; suit < 4; suit++) if ((mask & (1 << suit)) != 0)
                    {
                        int c = suit * 13 + rank; if (owners[c] != seat) { valid = false; break; }
                        if (c < 27) lo |= 1 << c; else hi |= 1 << (c - 27); n++;
                    }
                    if (!valid) continue;
                    if (n == count && TryPlay(seat, lo, hi)) return true;
                    if (n == count - 1 && owners[52] == seat && TryPlay(seat, lo, hi | (1 << 25))) return true;
                }
            }
        }
        if (owners[52] == seat && TryPlay(seat, 0, 1 << 25)) return true;
        return TryPass(seat);
    }
    public void Deal()
    {
        int oldPhase = phase;
        for (int s = 0; s < 4; s++) { previousPlaces[s] = oldPhase == 2 ? places[s] : 0; places[s] = 0; passed[s] = false; }
        int[] deck = new int[53]; for (int c = 0; c < 53; c++) deck[c] = c;
        for (int i = 52; i > 0; i--) { int j = Random.Range(0, i + 1); int t = deck[i]; deck[i] = deck[j]; deck[j] = t; }
        int seat = FirstSeat();
        for (int i = 0; i < 53; i++)
        {
            owners[deck[i]] = seat;
            do { seat = (seat + 1) % 4; } while (seats[seat] == 0);
        }
        phase = 1; finished = 0; penaltyCount = 0; disqualified = new bool[4]; revolution = false; jackBack = false;
        pending = 0; giveRemaining = 0; discardRemaining = 0; bombRemaining = 0; skipCount = 0; clearAfter = false;
        pileCount = 0; pileRank = -1; pileSuit = 0; lockSuit = 0; pileText = ""; pileStraight = false; pileJoker = false; lastSeat = -1;
        turn = owners[26]; // diamond three
        roundLead = turn;
        if (oldPhase == 2)
        {
            int worst = 0; for (int s = 0; s < 4; s++) if (seats[s] != 0 && previousPlaces[s] > worst) { worst = previousPlaces[s]; roundLead = s; }
            exchangeStep = 0; phase = 3; NextExchange();
        }
        else message = "配札しました。ダイヤ3を持つ人から開始";
        soundEvent = 4;
    }
    private void NextExchange()
    {
        int pairs = PlayerCount() >= 4 ? 2 : 1;
        while (exchangeStep < pairs)
        {
            int rich = -1, poor = -1, best = exchangeStep + 1, worst = PlayerCount() - exchangeStep;
            for (int s = 0; s < 4; s++) if (seats[s] != 0) { if (previousPlaces[s] == best) rich = s; if (previousPlaces[s] == worst) poor = s; }
            exchangeStep++;
            if (rich < 0 || poor < 0) continue;
            int count = exchangeStep == 1 ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                int card = -1, strength = -1;
                for (int c = 0; c < 53; c++) if (owners[c] == poor) { int r = c == 52 ? 13 : c % 13; if (r > strength) { strength = r; card = c; } }
                if (card >= 0) owners[card] = rich;
            }
            pending = 4; pendingCount = count; pendingSeat = rich; receiver = poor; turn = rich;
            message = names[rich] + "：交換するカードを" + count.ToString() + "枚選んで確定"; return;
        }
        pending = 0; phase = 1; turn = roundLead; message = "交換完了。前の最下位から開始";
    }
    public string CardName(int c)
    {
        if (c == 52) return "JOKER";
        string suit = c / 13 == 0 ? "♠" : c / 13 == 1 ? "♥" : c / 13 == 2 ? "♦" : "♣";
        int r = c % 13 + 3;
        string rank = r == 11 ? "J" : r == 12 ? "Q" : r == 13 ? "K" : r == 14 ? "A" : r == 15 ? "2" : r.ToString();
        return suit + rank;
    }
    private bool MaskHas(int c, int lo, int hi) { return c < 27 ? (lo & (1 << c)) != 0 : (hi & (1 << (c - 27))) != 0; }
    public bool TryPlay(int seat, int lo, int hi)
    {
        if (seat < 0 || seat >= 4 || (phase != 1 && phase != 3) || seat != turn || lo < 0 || hi < 0 || (lo >> 27) != 0 || (hi >> 26) != 0) return false;
        int n = 0;
        for (int c = 0; c < 53; c++) if (MaskHas(c, lo, hi)) { if (owners[c] != seat) return false; n++; }
        if (n == 0) return false;
        if (pending != 0)
        {
            if (pending == 3 || n != pendingCount || seat != pendingSeat) return false;
            for (int c = 0; c < 53; c++) if (MaskHas(c, lo, hi)) owners[c] = pending == 1 ? -1 : receiver;
            if (pending == 4) { NextExchange(); return true; }
            if (pending == 1) discardRemaining = 0; else giveRemaining = 0;
            pending = 0; ContinueEffects(); return true;
        }
        if (passed[seat]) return false;
        bool joker = MaskHas(52, lo, hi), group = true, straight = false;
        int rank = -1, min = 13, max = -1, suit = 0, suitIndex = -1, natural = 0;
        bool sameSuit = true;
        int ten = 0, queen = 0, seven = 0, five = 0; bool eight = false, jack = false;
        for (int c = 0; c < 52; c++) if (MaskHas(c, lo, hi))
        {
            int r = c % 13, s = c / 13;
            if (rank >= 0 && rank != r) group = false;
            rank = r; min = Mathf.Min(min, r); max = Mathf.Max(max, r);
            if (suitIndex >= 0 && suitIndex != s) sameSuit = false;
            suitIndex = s; suit |= 1 << s; natural++;
            if (r == 7) ten++; if (r == 9) queen++; if (r == 4) seven++; if (r == 2) five++; if (r == 5) eight = true; if (r == 8) jack = true;
        }
        bool reversed = revolution != jackBack;
        if (n == 1 && joker) rank = 13;
        if (!group)
        {
            if (!Rule(6) || n < 3 || !sameSuit || max - min + 1 > n) return false;
            if (max - min + 1 < n)
            {
                // Joker extends one end, choosing the strongest valid interpretation.
                if (reversed && min > 0) min--; else if (!reversed && max < 12) max++; else if (min > 0) min--; else return false;
            }
            if (max - min + 1 != n || natural + (joker ? 1 : 0) != n) return false;
            straight = true; rank = min;
        }
        if (group && n > 5) return false;
        bool spadeReturn = Rule(8) && pileCount == 1 && pileJoker && n == 1 && MaskHas(0, lo, hi);
        if (pileCount > 0 && !spadeReturn)
        {
            if (pileJoker || n != pileCount || straight != pileStraight) return false;
            if (!joker || n != 1)
            {
                if (reversed ? rank >= pileRank : rank <= pileRank) return false;
            }
            if (lockSuit != 0 && suit != lockSuit)
            {
                int missing = lockSuit & (15 ^ suit);
                if (!joker || (suit & (15 ^ lockSuit)) != 0 || missing == 0 || (missing & (missing - 1)) != 0) return false;
                suit = lockSuit;
            }
        }
        bool forbidden = false;
        if (Rule(11) && n == HandCount(seat))
        {
            forbidden = joker || eight;
            for (int c = 0; c < 52; c++) if (MaskHas(c, lo, hi) && c % 13 == (revolution ? 0 : 12)) forbidden = true;
        }
        effectMask = 0; soundEvent = 1;
        if (Rule(4) && pileCount > 0 && lockSuit == 0 && suit != 0 && suit == pileSuit && !joker && !pileJoker)
        { lockSuit = suit; effectMask |= 1 << 4; }
        pileCount = n; pileRank = rank; pileSuit = suit; pileStraight = straight; pileJoker = n == 1 && joker;
        pileText = "";
        for (int c = 0; c < 53; c++) if (MaskHas(c, lo, hi)) { owners[c] = -1; pileText += CardName(c) + "  "; }
        lastSeat = seat;
        if (forbidden) { disqualified[seat] = true; places[seat] = PlayerCount() - penaltyCount; penaltyCount++; }
        if (straight) effectMask |= 1 << 6;
        if ((Rule(5) && group && n >= 4) || (Rule(7) && straight && n >= 4))
        { revolution = !revolution; effectMask |= 1 << (straight ? 7 : 5); }
        if (Rule(2) && jack) { jackBack = !jackBack; effectMask |= 1 << 2; }
        if (Rule(3) && eight) effectMask |= 1 << 3;
        if (spadeReturn) effectMask |= 1 << 8;
        giveRemaining = Rule(9) ? seven : 0; discardRemaining = Rule(0) ? ten : 0; bombRemaining = Rule(1) ? queen : 0;
        skipCount = Rule(10) ? five : 0;
        if (giveRemaining > 0) effectMask |= 1 << 9;
        if (discardRemaining > 0) effectMask |= 1;
        if (bombRemaining > 0) effectMask |= 1 << 1;
        if (skipCount > 0) effectMask |= 1 << 10;
        clearAfter = (Rule(3) && eight) || spadeReturn;
        if (forbidden) { effectMask |= 1 << 11; giveRemaining = 0; discardRemaining = 0; bombRemaining = 0; skipCount = 0; clearAfter = true; }
        pendingSeat = seat; message = names[seat] + " が " + pileText + "を出しました";
        ContinueEffects(); return true;
    }
    private void ContinueEffects()
    {
        int seat = pendingSeat;
        if (giveRemaining > 0 && HandCount(seat) > 0)
        {
            receiver = NextActive(seat);
            if (receiver >= 0 && receiver != seat) { pending = 2; pendingCount = Mathf.Min(giveRemaining, HandCount(seat)); message = names[seat] + "：次の相手に渡す" + pendingCount.ToString() + "枚を選択"; return; }
        }
        giveRemaining = 0;
        if (discardRemaining > 0 && HandCount(seat) > 0)
        { pending = 1; pendingCount = Mathf.Min(discardRemaining, HandCount(seat)); message = names[seat] + "：捨てる" + pendingCount.ToString() + "枚を選択"; return; }
        discardRemaining = 0;
        if (bombRemaining > 0) { pending = 3; pendingCount = bombRemaining; message = names[seat] + "：12ボンバーの数字を選択"; return; }
        pending = 0; RankEmpty(seat);
        if (phase == 2) return;
        if (clearAfter) { ClearTrick(seat); return; }
        Advance(seat, skipCount); skipCount = 0;
    }
    public bool TryBomb(int seat, int rank)
    {
        if (phase != 1 || pending != 3 || pendingSeat != seat || rank < 0 || rank > 12) return false;
        for (int c = 0; c < 52; c++) if (c % 13 == rank) owners[c] = -1;
        bombRemaining--; effectMask = 1 << 1; soundEvent = 1;
        ContinueEffects(); return true;
    }
    private void RankEmpty(int first)
    {
        for (int i = 0; i < 4; i++) { int s = (first + i + 4) % 4; if (seats[s] != 0 && places[s] == 0 && HandCount(s) == 0) { finished++; places[s] = finished; } }
        int left = 0, last = -1;
        for (int s = 0; s < 4; s++) if (Active(s)) { left++; last = s; }
        if (left <= 1)
        {
            if (last >= 0) { finished++; places[last] = finished; }
            phase = 2; turn = -1; pending = 0; soundEvent = 3; message = "対戦終了！ 次の対戦では順位に応じてカードを交換します";
        }
    }
    private void ClearTrick(int lead)
    {
        pileCount = 0; pileRank = -1; pileSuit = 0; lockSuit = 0; pileStraight = false; pileJoker = false; pileText = "";
        jackBack = false; clearAfter = false; skipCount = 0;
        for (int s = 0; s < 4; s++) passed[s] = false;
        turn = Active(lead) ? lead : NextActive(lead); lastSeat = -1;
        message = "場が流れました。" + (turn >= 0 ? names[turn] : "") + " から";
    }
    private void Advance(int after, int skips)
    {
        int next = after;
        for (int k = 0; k <= skips; k++)
        {
            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                next = NextActive(next);
                if (next < 0 || (next == lastSeat && skips == 0)) { ClearTrick(lastSeat); return; }
                if (!passed[next]) { found = true; break; }
            }
            if (!found) { ClearTrick(lastSeat); return; }
        }
        turn = next;
    }
    public bool TryPass(int seat)
    {
        if (phase != 1 || pending != 0 || turn != seat || pileCount == 0) return false;
        passed[seat] = true; effectMask = 0; soundEvent = 2; message = names[seat] + " がパス";
        Advance(seat, 0); return true;
    }
    private void RemoveSeat(int seat)
    {
        for (int c = 0; c < 53; c++) if (owners[c] == seat) owners[c] = -1;
        if (places[seat] > 0) { if (disqualified[seat]) penaltyCount--; else finished--; int oldRank = places[seat]; for (int s = 0; s < 4; s++) if (places[s] > oldRank) places[s]--; }
        seats[seat] = 0; disqualified[seat] = false; places[seat] = 0; previousPlaces[seat] = 0;
        if (phase == 3) { pending = 0; phase = 0; turn = -1; message = "交換中の退出があったため、待機室に戻りました"; }
        if (phase == 1)
        {
            RankEmpty(seat);
            if (phase == 1)
            {
                if (pendingSeat == seat) { pending = 0; giveRemaining = 0; discardRemaining = 0; bombRemaining = 0; }
                if (pending != 0 && receiver == seat) { giveRemaining = 0; pending = 0; ContinueEffects(); }
                if (turn == seat || lastSeat == seat) ClearTrick(seat);
            }
        }
        if (FirstSeat() < 0) { for (int s = 0; s < 4; s++) seats[s] = 0; phase = 0; pending = 0; turn = -1; message = "参加を押してください（1～4人）"; }
    }

    // Per-client UI: hands are never printed in another participant's view.
    private void SendAction(int action, int a, int b)
    {
        if (table == null) return;
        int rev = (int)table.GetProgramVariable("revision");
        table.SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestAction", action, a, b, rev);
    }
    public void AddComputer() { SendAction(8, 0, 0); }
    public void RuleBook() { bookPanel.SetActive(!bookPanel.activeSelf); DrawBook(); }
    public void NextPage() { bookPage = (bookPage + 1) % bookPages.Length; DrawBook(); }
    public void PreviousPage() { bookPage = (bookPage + bookPages.Length - 1) % bookPages.Length; DrawBook(); }
    private void DrawBook() { bookLabel.text = bookPages[bookPage] + "\n\n" + (bookPage + 1).ToString() + " / " + bookPages.Length.ToString(); }
    public void Join() { SendAction(0, 0, 0); }
    public void Leave() { SendAction(1, 0, 0); }
    public void Begin() { SendAction(2, 0, 0); }
    public void Pass() { SendAction(4, 0, 0); }
    public void Lobby() { SendAction(7, 0, 0); }
    public void Play()
    {
        int lo = 0, hi = 0;
        for (int c = 0; c < 53; c++) if (selected[c]) { if (c < 27) lo |= 1 << c; else hi |= 1 << (c - 27); }
        SendAction(3, lo, hi);
        hintLabel.text = "出せない場合は枚数・強さ・縛りを確認";
    }
    public void Settings() { settingsPanel.SetActive(!settingsPanel.activeSelf); _Render(); }
    public void ToggleVoice()
    {
        bool enabled = !(bool)table.GetProgramVariable("voiceEnabled");
        table.SetProgramVariable("voiceEnabled", (object)enabled); table.SendCustomEvent("_AudioSettings"); _Render();
    }
    public void ToggleSound()
    {
        bool enabled = !(bool)table.GetProgramVariable("soundEnabled");
        table.SetProgramVariable("soundEnabled", (object)enabled); table.SendCustomEvent("_AudioSettings"); _Render();
    }
    public void _AudioSettings() { if (!voiceEnabled) { queuedVoices = 0; voiceSource.Stop(); } NotifyViews(); }
    public void BackToMenu()
    {
        // Participants finish/leave before closing the shared session.
        if ((int)table.GetProgramVariable("phase") == 1 || (int)table.GetProgramVariable("phase") == 3) { hintLabel.text = "対戦中です。終了するか、参加者が退出してから戻ってください"; return; }
        if (sessionController != null) sessionController.SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestClose");
        else gameObject.SetActive(false);
    }
    private void ToggleCard(int c) { selected[c] = !selected[c]; DrawHand(); }
    private void ToggleRule(int bit) { SendAction(5, bit, 0); }
    private void Bomb(int rank) { SendAction(6, rank, 0); }
    public void _Render()
    {
        if (table == null || statusLabel == null || Networking.LocalPlayer == null) return;
        int rev = (int)table.GetProgramVariable("revision");
        int t = (int)table.GetProgramVariable("turn");
        int state = (int)table.GetProgramVariable("phase");
        int[] ids = (int[])table.GetProgramVariable("seats");
        int mine = -1, host = -1;
        for (int s = 0; s < 4; s++) { if (ids[s] > 0 && host < 0) host = s; if (ids[s] == Networking.LocalPlayer.playerId) mine = s; }
        if (seenRevision != rev)
        {
            for (int c = 0; c < 53; c++) selected[c] = false;
            if (table == self && seenRevision >= 0)
            {
                if (voiceEnabled) queuedVoices |= effectMask;
                if (soundEnabled)
                {
                    if (state == 1 && t == mine && t != seenTurn) effectsSource.PlayOneShot(turnSound);
                    else if (soundEvent == 1) effectsSource.PlayOneShot(cardSound);
                    else if (soundEvent == 2) effectsSource.PlayOneShot(passSound);
                    else if (soundEvent == 3 || soundEvent == 4) effectsSource.PlayOneShot(winSound);
                }
            }
            seenRevision = rev; seenTurn = t; hintLabel.text = "カードを選択 → 出す / パス";
        }
        statusLabel.text = (string)table.GetProgramVariable("message");
        string[] ns = (string[])table.GetProgramVariable("names");
        int[] ps = (int[])table.GetProgramVariable("places");
        int[] os = (int[])table.GetProgramVariable("owners");
        bool[] passes = (bool[])table.GetProgramVariable("passed");
        playersLabel.text = "";
        int totalSeats = 0; for (int s = 0; s < 4; s++) if (ids[s] != 0) totalSeats++;
        for (int s = 0; s < 4; s++) if (ids[s] != 0)
        {
            int count = 0; for (int c = 0; c < 53; c++) if (os[c] == s) count++;
            playersLabel.text += (((bool[])table.GetProgramVariable("disqualified"))[s] ? "反則 " : "") + (s == t ? "▶ " : "   ") + ns[s] + (s == mine ? "（あなた）" : "") + "   " + (state == 0 ? "参加中" : ps[s] > 0 ? ps[s].ToString() + "位 / " + (ps[s] == 1 ? "大富豪" : ps[s] == totalSeats ? "大貧民" : totalSeats >= 4 && ps[s] == 2 ? "富豪" : totalSeats >= 4 && ps[s] == totalSeats - 1 ? "貧民" : "平民") : count.ToString() + "枚" + (passes[s] ? " / PASS" : "")) + "\n";
        }
        int others = 0;
        for (int s = 0; s < 4; s++) if (ids[s] != 0 && s != mine) others++;
        int tile = 0;
        for (int s = 0; s < 4; s++)
        {
            if (ids[s] == 0 || s == mine) continue;
            int remaining = 0; for (int c = 0; c < 53; c++) if (os[c] == s) remaining++;
            var rect = opponentTiles[tile].GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2((tile - (others - 1) * .5f) * (1760f / Mathf.Max(1, others)), 238f);
            rect.sizeDelta = new Vector2(1760f / Mathf.Max(1, others) - 20f, 160f);
            opponentTiles[tile].SetActive(true);
            opponentTiles[tile].GetComponent<Image>().color = s == t ? new Color(.65f,.87f,.71f) : Color.white;
            string display = ns[s]; if (display.Length > 12) display = display.Substring(0,12) + "…";
            opponentNames[tile].text = (s == t ? "▶ " : "") + display;
            opponentCounts[tile].text = ps[s] > 0 ? ps[s].ToString() + " 位" : "残り " + remaining.ToString() + " 枚" + (passes[s] ? " / パス" : "");
            tile++;
        }
        for (int i = tile; i < 4; i++) opponentTiles[i].SetActive(false);
        bool reversed = (bool)table.GetProgramVariable("revolution") != (bool)table.GetProgramVariable("jackBack");
        sharedPileLabel.text = state == 0 ? "大富豪 / 参加待ち" : (int)table.GetProgramVariable("pileCount") == 0 ? "場にカードはありません" : (string)table.GetProgramVariable("pileText");
        int locked = (int)table.GetProgramVariable("lockSuit");
        string suits = "";
        if ((locked & 1) != 0) suits += "♠ ";
        if ((locked & 2) != 0) suits += "♥ ";
        if ((locked & 4) != 0) suits += "♦ ";
        if ((locked & 8) != 0) suits += "♣ ";
        sharedStateLabel.text = "縛り：" + (locked == 0 ? "なし" : suits) + "    Jバック：" + ((bool)table.GetProgramVariable("jackBack") ? "発動中" : "なし") + "\n革命：" + ((bool)table.GetProgramVariable("revolution") ? "発動中" : "なし") + "    " + (reversed ? "2 → A → … → 4 → 3 が強い" : "3 → 4 → … → A → 2 が強い");
        string currentName = t >= 0 && t < 4 ? ns[t] : "";
        if (currentName.Length > 18) currentName = currentName.Substring(0,18) + "…";
        sharedTurnLabel.text = state == 0 ? "参加 → 配札で開始" : state == 2 ? "対戦終了" : (state == 3 ? "カード交換 / " : "手番 / ") + currentName;
        tableLabel.text = ((int)table.GetProgramVariable("pileCount") == 0 ? "場にカードはありません" : (string)table.GetProgramVariable("pileText")) + "\n" + (reversed ? "弱い数字が強い" : "3 → 4 → … → A → 2") + ((int)table.GetProgramVariable("lockSuit") != 0 ? "   / 縛り中" : "") + ((bool)table.GetProgramVariable("revolution") ? "   / 革命" : "") + ((bool)table.GetProgramVariable("jackBack") ? "   / Jバック" : "");
        bool openSeat = false; for (int s = 0; s < 4; s++) if (ids[s] <= 0) openSeat = true;
        joinButton.interactable = state == 0 && mine < 0 && openSeat;
        leaveButton.interactable = mine >= 0;
        startButton.interactable = mine >= 0 && mine == host && (state == 0 || state == 2);
        int pend = (int)table.GetProgramVariable("pending");
        playButton.interactable = mine >= 0 && mine == t && (state == 1 || state == 3) && pend != 3;
        addCpuButton.interactable = mine >= 0 && mine == host && state == 0;
        addCpuButton.GetComponentInChildren<Text>().text = (bool)table.GetProgramVariable("cpuEnabled") ? "CPU補充 ON" : "CPU補充 OFF";
        passButton.interactable = mine >= 0 && mine == t && state == 1 && pend == 0 && (int)table.GetProgramVariable("pileCount") > 0;
        bombPanel.SetActive(pend == 3 && mine == t);
        int bits = (int)table.GetProgramVariable("rules");
        for (int i = 0; i < 12; i++) { ruleLabels[i].text = ((bits & (1 << i)) != 0 ? "● ON   " : "○ OFF   ") + ruleNames[i]; ruleButtons[i].interactable = state == 0 && (mine == host && mine >= 0); }
        settingsLabel.text = "待機中にホストが変更 / 全員に反映\n解説 " + ((bool)table.GetProgramVariable("voiceEnabled") ? "ON" : "OFF") + "   効果音 " + ((bool)table.GetProgramVariable("soundEnabled") ? "ON" : "OFF") + "（音声設定は自分だけ）\nVOICEVOX:ずんだもん";
        DrawHand();
    }
    private void DrawHand()
    {
        int mine = -1; int[] ids = (int[])table.GetProgramVariable("seats");
        for (int s = 0; s < 4; s++) if (ids[s] == Networking.LocalPlayer.playerId) mine = s;
        int[] os = (int[])table.GetProgramVariable("owners");
        int state = (int)table.GetProgramVariable("phase");
        int slot = 0, count = 0, handSize = 0;
        for (int c = 0; c < 53; c++) if (mine >= 0 && os[c] == mine) handSize++;
        // Rank-major ordering makes groups/straights easy to select.
        for (int k = 0; k < 53; k++)
        {
            int c = k == 52 ? 52 : (k % 4) * 13 + k / 4;
            bool visible = mine >= 0 && os[c] == mine && state != 0;
            cardButtons[c].gameObject.SetActive(visible);
            if (!visible) continue;
            var rect = cardButtons[c].GetComponent<RectTransform>();
            float unit = handSize <= 1 ? 0f : (float)slot / (handSize - 1) * 2f - 1f;
            float spread = Mathf.Min(780f, Mathf.Max(0, handSize - 1) * 62f);
            rect.anchoredPosition = new Vector2(unit * spread, -250f - unit * unit * 27f + (selected[c] ? 40f : 0f));
            rect.localEulerAngles = new Vector3(0f, 0f, -unit * 12f);
            rect.SetSiblingIndex(slot);
            cardLabels[c].text = c == 52 ? "JK\n★" : CardName(c).Substring(1) + "\n" + CardName(c).Substring(0,1);
            cardButtons[c].image.color = selected[c] ? new Color(0.7f,0.87f,0.78f) : Color.white;
            cardLabels[c].color = c < 52 && (c / 13 == 1 || c / 13 == 2) ? new Color(.65f,.18f,.17f) : new Color(.12f,.15f,.14f);
            slot++; if (selected[c]) count++;
        }
        handLabel.text = mine < 0 ? "観戦中 / 参加すると自分の手札が表示されます" : (mine == (int)table.GetProgramVariable("turn") ? "▶ あなたの番   " : "手札   ") + slot.ToString() + "枚    選択 " + count.ToString() + "枚";
    }
    public void Update()
    {
        if (table == self && Networking.IsOwner(gameObject) && (phase == 1 || phase == 3) && turn >= 0 && seats[turn] < 0)
        {
            cpuElapsed += Time.deltaTime;
            if (cpuElapsed >= 1.2f) { cpuElapsed = 0f; if (CpuStep()) Commit(); }
        }
        else cpuElapsed = 0f;
        if (table != self || !voiceEnabled || voiceSource == null || voiceSource.isPlaying) return;
        for (int i = 0; i < 12; i++) if ((queuedVoices & (1 << i)) != 0)
        { queuedVoices ^= 1 << i; if (ruleVoices[i] != null) { voiceSource.clip = ruleVoices[i]; voiceSource.Play(); } return; }
    }
    public void Card0() { ToggleCard(0); }
    public void Card1() { ToggleCard(1); }
    public void Card2() { ToggleCard(2); }
    public void Card3() { ToggleCard(3); }
    public void Card4() { ToggleCard(4); }
    public void Card5() { ToggleCard(5); }
    public void Card6() { ToggleCard(6); }
    public void Card7() { ToggleCard(7); }
    public void Card8() { ToggleCard(8); }
    public void Card9() { ToggleCard(9); }
    public void Card10() { ToggleCard(10); }
    public void Card11() { ToggleCard(11); }
    public void Card12() { ToggleCard(12); }
    public void Card13() { ToggleCard(13); }
    public void Card14() { ToggleCard(14); }
    public void Card15() { ToggleCard(15); }
    public void Card16() { ToggleCard(16); }
    public void Card17() { ToggleCard(17); }
    public void Card18() { ToggleCard(18); }
    public void Card19() { ToggleCard(19); }
    public void Card20() { ToggleCard(20); }
    public void Card21() { ToggleCard(21); }
    public void Card22() { ToggleCard(22); }
    public void Card23() { ToggleCard(23); }
    public void Card24() { ToggleCard(24); }
    public void Card25() { ToggleCard(25); }
    public void Card26() { ToggleCard(26); }
    public void Card27() { ToggleCard(27); }
    public void Card28() { ToggleCard(28); }
    public void Card29() { ToggleCard(29); }
    public void Card30() { ToggleCard(30); }
    public void Card31() { ToggleCard(31); }
    public void Card32() { ToggleCard(32); }
    public void Card33() { ToggleCard(33); }
    public void Card34() { ToggleCard(34); }
    public void Card35() { ToggleCard(35); }
    public void Card36() { ToggleCard(36); }
    public void Card37() { ToggleCard(37); }
    public void Card38() { ToggleCard(38); }
    public void Card39() { ToggleCard(39); }
    public void Card40() { ToggleCard(40); }
    public void Card41() { ToggleCard(41); }
    public void Card42() { ToggleCard(42); }
    public void Card43() { ToggleCard(43); }
    public void Card44() { ToggleCard(44); }
    public void Card45() { ToggleCard(45); }
    public void Card46() { ToggleCard(46); }
    public void Card47() { ToggleCard(47); }
    public void Card48() { ToggleCard(48); }
    public void Card49() { ToggleCard(49); }
    public void Card50() { ToggleCard(50); }
    public void Card51() { ToggleCard(51); }
    public void Card52() { ToggleCard(52); }
    public void Rule0() { ToggleRule(0); }
    public void Rule1() { ToggleRule(1); }
    public void Rule2() { ToggleRule(2); }
    public void Rule3() { ToggleRule(3); }
    public void Rule4() { ToggleRule(4); }
    public void Rule5() { ToggleRule(5); }
    public void Rule6() { ToggleRule(6); }
    public void Rule7() { ToggleRule(7); }
    public void Rule8() { ToggleRule(8); }
    public void Rule9() { ToggleRule(9); }
    public void Rule10() { ToggleRule(10); }
    public void Rule11() { ToggleRule(11); }
    public void Bomb0() { Bomb(0); }
    public void Bomb1() { Bomb(1); }
    public void Bomb2() { Bomb(2); }
    public void Bomb3() { Bomb(3); }
    public void Bomb4() { Bomb(4); }
    public void Bomb5() { Bomb(5); }
    public void Bomb6() { Bomb(6); }
    public void Bomb7() { Bomb(7); }
    public void Bomb8() { Bomb(8); }
    public void Bomb9() { Bomb(9); }
    public void Bomb10() { Bomb(10); }
    public void Bomb11() { Bomb(11); }
    public void Bomb12() { Bomb(12); }
}
