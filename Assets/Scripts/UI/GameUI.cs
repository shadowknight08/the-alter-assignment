using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using CardGame.Gameplay.Cards;
using CardGame.Gameplay.Match;

namespace CardGame.UI
{
    // Main game UI - singleton for easy access
    public class GameUI : MonoBehaviour
    {
        private static GameUI instance;
        public static GameUI Instance => instance;

        [Header("Hand Display")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardPrefab;

        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI handCountText;

        [Header("Opponent Info")]
        [SerializeField] private TextMeshProUGUI opponentScoreText;

        [Header("Turn Info")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Slider timerSlider;

        [Header("Action Buttons")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button passButton;

        [Header("Turn Results")]
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TextMeshProUGUI resultsText;

        [Header("Match End")]
        [SerializeField] private GameObject matchEndPanel;
        [SerializeField] private TextMeshProUGUI matchEndText;

        private readonly List<int> selectedCardIds = new();
        private readonly Dictionary<int, CardUI> cardUIs = new();
        private List<int> currentHand = new();
        private int currentEnergy;
        private int currentScore;
        private int opponentScore;
        private int currentTurn;
        private float turnTimer;
        private bool isSubmitting;
        private MatchController matchController;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void Start()
        {
            matchController = FindObjectOfType<MatchController>();
            if (matchController == null)
            {
                Debug.LogError("[GameUI] MatchController not found!");
            }

            SetupButtons();
            SubscribeToEvents();
            HidePanels();
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (turnTimer > 0f)
            {
                turnTimer -= Time.deltaTime;
                UpdateTimerDisplay();
            }
        }

        private void SetupButtons()
        {
            if (submitButton != null)
                submitButton.onClick.AddListener(OnSubmitClicked);

            if (passButton != null)
                passButton.onClick.AddListener(OnPassClicked);
        }

        private void HidePanels()
        {
            if (resultsPanel != null)
                resultsPanel.SetActive(false);

            if (matchEndPanel != null)
                matchEndPanel.SetActive(false);
        }

        private void SubscribeToEvents()
        {
            GameEventHub.OnTurnStarted += HandleTurnStarted;
            GameEventHub.OnMatchEnded += HandleMatchEnded;
        }

        private void UnsubscribeFromEvents()
        {
            GameEventHub.OnTurnStarted -= HandleTurnStarted;
            GameEventHub.OnMatchEnded -= HandleMatchEnded;
        }

        private void HandleTurnStarted(int turn) => OnTurnStarted(turn);
        private void HandleMatchEnded(MatchResult result)
        {
            bool isWinner = NetworkManager.Singleton != null &&
                           NetworkManager.Singleton.LocalClientId == result.WinnerId;
            ShowMatchEnd(isWinner, result.WinnerScore, result.LoserScore);
        }

        public void OnSubmissionAccepted()
        {
            isSubmitting = true;
            UpdateUI();
        }

        public void OnSubmissionRejected(string reason)
        {
            isSubmitting = false;
            Debug.LogWarning($"[GameUI] Submission rejected: {reason}");
            UpdateUI();
        }

        public void OnHandUpdated(int[] cardIds)
        {
            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            int cardCount = cardIds?.Length ?? 0;
            Debug.Log($"[GameUI] Client {localClientId} received hand update: {cardCount} cards");

            currentHand = new List<int>(cardIds ?? System.Array.Empty<int>());

            if (cardCount > 10)
            {
                Debug.LogWarning($"[GameUI] WARNING: Received unusually large hand with {cardCount} cards! This might indicate a bug.");
            }

            RefreshHandDisplay();
            UpdateUI();
        }

        public void OnEnergyUpdated(int energy)
        {
            currentEnergy = energy;
            UpdateUI();
        }

        public void OnScoreUpdated(int score)
        {
            currentScore = score;
            UpdateUI();
        }

        public void OnTurnStarted(int turnNumber)
        {
            currentTurn = turnNumber;
            turnTimer = 30f;
            selectedCardIds.Clear();
            isSubmitting = false;
            RefreshHandDisplay();
            UpdateUI();

            if (resultsPanel != null)
                resultsPanel.SetActive(false);
        }

        public void OnTurnResolved(int turnNumber, ulong[] playerIds, int[] powerApplied, int[] scores)
        {
            isSubmitting = false;
            ShowTurnResults(playerIds, powerApplied, scores);
        }

        public void ShowMatchEnd(bool isWinner, int winnerScore, int loserScore)
        {
            if (matchEndPanel == null || matchEndText == null) return;

            string message = isWinner
                ? $"Victory!\nYou Won {winnerScore} - {loserScore}"
                : $"Defeat!\nYou Lost {loserScore} - {winnerScore}";

            matchEndText.text = message;
            matchEndPanel.SetActive(true);

            if (submitButton != null) submitButton.interactable = false;
            if (passButton != null) passButton.interactable = false;
        }

        private void HandleSubmissionAccepted() => OnSubmissionAccepted();
        private void HandleSubmissionRejected(string reason) => OnSubmissionRejected(reason);

        private void RefreshHandDisplay()
        {
            if (handContainer != null)
            {
                for (int i = handContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = handContainer.GetChild(i);
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
            cardUIs.Clear();

            if (handContainer == null || cardPrefab == null)
            {
                Debug.LogWarning("[GameUI] Hand container or card prefab is null!");
                return;
            }

            if (currentHand == null)
            {
                Debug.LogWarning("[GameUI] Current hand is null!");
                return;
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            Debug.Log($"[GameUI] Refreshing hand display for client {localClientId}: {currentHand.Count} cards");

            int cardIndex = 0;
            foreach (int cardId in currentHand)
            {
                if (CardDatabase.Instance.TryGetCard(cardId, out var card))
                {
                    GameObject cardObj = Instantiate(cardPrefab, handContainer);
                    cardObj.SetActive(true);
                    CardUI cardUI = cardObj.GetComponent<CardUI>();
                    if (cardUI == null)
                        cardUI = cardObj.AddComponent<CardUI>();

                    cardUI.Initialize(card, this);

                    if (!cardUIs.ContainsKey(cardId))
                    {
                        cardUIs[cardId] = cardUI;
                    }
                    cardIndex++;
                }
                else
                {
                    Debug.LogWarning($"[GameUI] Card ID {cardId} not found in database!");
                }
            }

            Debug.Log($"[GameUI] Created {cardIndex} card UI objects from {currentHand.Count} cards in hand");

            UpdateCardSelection();
        }

        public void OnCardClicked(int cardId)
        {
            if (isSubmitting) return;

            if (selectedCardIds.Contains(cardId))
            {
                selectedCardIds.Remove(cardId);
            }
            else if (CardDatabase.Instance.TryGetCard(cardId, out var card))
            {
                int totalCost = GetSelectedCardsCost() + card.cost;
                if (totalCost <= currentEnergy)
                {
                    selectedCardIds.Add(cardId);
                }
            }

            UpdateCardSelection();
            UpdateUI();
        }

        private int GetSelectedCardsCost()
        {
            int total = 0;
            foreach (int cardId in selectedCardIds)
            {
                if (CardDatabase.Instance.TryGetCard(cardId, out var card))
                    total += card.cost;
            }
            return total;
        }

        private void UpdateCardSelection()
        {
            foreach (var kvp in cardUIs)
            {
                kvp.Value.SetSelected(selectedCardIds.Contains(kvp.Key));
            }
        }

        private void OnSubmitClicked()
        {
            if (isSubmitting || selectedCardIds.Count == 0) return;
            SubmitCards();
        }

        private void OnPassClicked()
        {
            if (isSubmitting) return;
            selectedCardIds.Clear();
            SubmitCards();
        }

        private void SubmitCards()
        {
            if (matchController == null) return;

            isSubmitting = true;
            matchController.SubmitCardsServerRpc(selectedCardIds.ToArray());
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (energyText != null)
                energyText.text = $"Energy: {currentEnergy}";

            if (scoreText != null)
                scoreText.text = $"Score: {currentScore}";

            if (opponentScoreText != null)
                opponentScoreText.text = $"Opponent: {opponentScore}";

            if (handCountText != null)
                handCountText.text = $"Hand: {currentHand.Count}";

            if (turnText != null)
                turnText.text = $"Turn {currentTurn}/6";

            int selectedCost = GetSelectedCardsCost();
            if (submitButton != null)
            {
                submitButton.interactable = !isSubmitting &&
                                           selectedCardIds.Count > 0 &&
                                           selectedCost <= currentEnergy;
            }

            if (passButton != null)
                passButton.interactable = !isSubmitting;
        }

        private void UpdateTimerDisplay()
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(turnTimer);
                timerText.text = $"{seconds}s";
            }

            if (timerSlider != null)
                timerSlider.value = turnTimer / 30f;
        }

        private void ShowTurnResults(ulong[] playerIds, int[] powerApplied, int[] scores)
        {
            if (resultsPanel == null || resultsText == null) return;

            ulong localId = NetworkManager.Singleton?.LocalClientId ?? 0;
            int localIndex = System.Array.IndexOf(playerIds, localId);

            if (localIndex < 0 || localIndex >= scores.Length) return;

            currentScore = scores[localIndex];
            int opponentIndex = (localIndex + 1) % playerIds.Length;

            string result = $"Turn {currentTurn} Results:\n";
            result += $"Your Power: {powerApplied[localIndex]}\n";
            result += $"Your Score: {currentScore}\n";

            if (opponentIndex < scores.Length)
            {
                result += $"Opponent Power: {powerApplied[opponentIndex]}\n";
                result += $"Opponent Score: {scores[opponentIndex]}\n";
                opponentScore = scores[opponentIndex];
            }

            resultsText.text = result;
            resultsPanel.SetActive(true);
            UpdateUI();
        }
    }
}
