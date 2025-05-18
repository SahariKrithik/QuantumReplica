using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("UI References")]
    public Slider betSlider;
    public TMP_Dropdown gameModeDropdown;
    public TMP_InputField betInputField;
    public TMP_Text winChanceText;
    public TMP_Text multiplierText;
    public TMP_Text resultText;
    public TMP_Text currencyDisplayText;
    public TextMeshProUGUI overUnderLabel;
    public Button placeBetButton;
    public TMP_Text betValidationMessage;

    public GameObject historyEntryPrefab;
    public Transform historyContentParent;
    [SerializeField] private ScrollRect historyScrollRect;
    [SerializeField] private ContentSizeFitter contentSizeFitter;

    private const int maxHistoryEntries = 30;
    private Queue<GameObject> entryPool = new Queue<GameObject>();

    private enum GameMode { RollUnder, RollOver }
    private GameMode currentMode = GameMode.RollUnder;

    private int playerBalance = 1000;
    private int vrfResult;

    private void Start()
    {
        UpdateCurrencyUI();

        // Optional failsafe to reset Viewport position
        historyScrollRect.viewport.localPosition = Vector3.zero;

        betSlider.onValueChanged.AddListener(delegate {
            UpdateChanceAndMultiplier();
            ValidateBetEligibility();
        });

        gameModeDropdown.onValueChanged.AddListener(OnGameModeChanged);
        betInputField.onValueChanged.AddListener(delegate { ValidateBetEligibility(); });

        betInputField.onSubmit.AddListener(delegate {
            if (placeBetButton.interactable)
            {
                OnPlaceBetButton();
            }
        });

        OnGameModeChanged(gameModeDropdown.value); // Initialize
        resultText.gameObject.SetActive(false);

        // Disable layout recalculations after initial setup
        StartCoroutine(DisableContentSizeFitter());
    }

    private IEnumerator DisableContentSizeFitter()
    {
        yield return new WaitForEndOfFrame();
        contentSizeFitter.enabled = false;
    }

    void OnGameModeChanged(int index)
    {
        currentMode = (index == 0) ? GameMode.RollUnder : GameMode.RollOver;

        betSlider.minValue = 1;
        betSlider.maxValue = 99;
        betSlider.value = 50;

        overUnderLabel.text = currentMode == GameMode.RollUnder
            ? "Mode: Roll Under"
            : "Mode: Roll Over";

        UpdateChanceAndMultiplier();
        ValidateBetEligibility();
    }

    public void OnPlaceBetButton()
    {
        int betAmount = int.Parse(betInputField.text);
        if (betAmount <= 0 || betAmount > playerBalance)
        {
            resultText.text = "Invalid Bet!";
            resultText.gameObject.SetActive(true);
            return;
        }

        int selectedNumber = Mathf.RoundToInt(betSlider.value);
        int winChance = (currentMode == GameMode.RollUnder)
            ? selectedNumber
            : 100 - selectedNumber;

        float multiplier = 100f / winChance;
        vrfResult = BlockchainService.SimulateVRFResult();

        bool isWin = currentMode == GameMode.RollUnder
            ? vrfResult < selectedNumber
            : vrfResult > selectedNumber;

        resultText.gameObject.SetActive(true);
        resultText.text = isWin
            ? $"Win! VRF: {vrfResult} → +{Mathf.RoundToInt(betAmount * multiplier)}"
            : $"Loss. VRF: {vrfResult} → -{betAmount}";

        if (isWin)
        {
            int payout = Mathf.RoundToInt(betAmount * multiplier);
            WalletService.Payout(payout);
            playerBalance += payout;
        }
        else
        {
            playerBalance -= betAmount;
        }

        // Reuse or create entry
        GameObject entry = GetPooledEntry();
        entry.transform.SetParent(historyContentParent, false);

        RectTransform entryRect = entry.GetComponent<RectTransform>();
        entryRect.anchorMin = new Vector2(0, entryRect.anchorMin.y);
        entryRect.anchorMax = new Vector2(1, entryRect.anchorMax.y);
        entryRect.offsetMin = new Vector2(0, entryRect.offsetMin.y);
        entryRect.offsetMax = new Vector2(0, entryRect.offsetMax.y);
        entryRect.anchoredPosition = new Vector2(0, entryRect.anchoredPosition.y);

        TMP_Text entryText = entry.GetComponent<TMP_Text>();
        entryText.text = $"Bet: {betAmount} | Result: {vrfResult} | {(isWin ? "WIN" : "LOSS")}";
        entryText.color = isWin ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.3f, 0.3f);

        // Limit total visible entries
        if (historyContentParent.childCount > maxHistoryEntries)
        {
            Transform oldest = historyContentParent.GetChild(0);
            ReturnEntryToPool(oldest.gameObject);
        }

        StartCoroutine(HighlightNewEntry(entry));
        StartCoroutine(ScrollToBottom());

        UpdateCurrencyUI();
        ValidateBetEligibility();
    }

    private GameObject GetPooledEntry()
    {
        if (entryPool.Count > 0)
        {
            GameObject pooled = entryPool.Dequeue();
            pooled.SetActive(true);
            return pooled;
        }

        return Instantiate(historyEntryPrefab);
    }

    private void ReturnEntryToPool(GameObject entry)
    {
        entry.SetActive(false);
        entry.transform.SetParent(null);
        entryPool.Enqueue(entry);
    }

    private void UpdateCurrencyUI()
    {
        currencyDisplayText.text = $"Balance: {playerBalance}";
    }

    private void UpdateChanceAndMultiplier()
    {
        int selectedNumber = Mathf.RoundToInt(betSlider.value);
        int winChance = currentMode == GameMode.RollUnder
            ? selectedNumber
            : 100 - selectedNumber;

        if (winChance <= 0)
        {
            multiplierText.text = $"Multiplier: ∞";
            winChanceText.text = $"Win Chance: 0%";
        }
        else
        {
            float multiplier = 100f / winChance;
            winChanceText.text = $"Win Chance: {winChance}%";
            multiplierText.text = $"Multiplier: x{multiplier:F2}";
        }
    }

    private void ValidateBetEligibility()
    {
        int selectedNumber = Mathf.RoundToInt(betSlider.value);
        string betText = betInputField.text;

        bool validNumberRange = selectedNumber > 0 && selectedNumber < 100;
        bool validBetAmount = int.TryParse(betText, out int betAmount)
                              && betAmount > 0
                              && betAmount <= playerBalance;

        bool canBet = validNumberRange && validBetAmount;
        placeBetButton.interactable = canBet;
        betValidationMessage.gameObject.SetActive(!canBet);
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        historyScrollRect.verticalNormalizedPosition = 0f;
    }

    private IEnumerator HighlightNewEntry(GameObject entry)
    {
        Image bg = entry.GetComponent<Image>();
        if (bg != null)
        {
            Color originalColor = bg.color;
            bg.color = Color.yellow;
            yield return new WaitForSeconds(0.5f);
            bg.color = originalColor;
        }
    }
}