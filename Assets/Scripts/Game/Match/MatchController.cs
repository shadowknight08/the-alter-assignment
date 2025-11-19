using System.Collections;
using System.Collections.Generic;
using CardGame.Gameplay.Cards;
using CardGame.UI;
using Unity.Netcode;
using UnityEngine;

namespace CardGame.Gameplay.Match
{
    // Server-side match controller - handles turns, submissions, card resolution
    public class MatchController : NetworkBehaviour
    {
        [SerializeField] private float turnDurationSeconds = 30f;
        [SerializeField] private int totalTurns = 6;
        [SerializeField] private int startingHandSize = 3;
        [SerializeField] private int maxEnergy = 6;
        [SerializeField] private int deckSize = 12;

        private readonly Dictionary<ulong, PlayerMatchState> playerStates = new();
        private readonly Dictionary<ulong, TurnSubmission> turnSubmissions = new();
        private readonly List<ulong> playerOrder = new();

        private Coroutine matchCoroutine;
        private float turnTimer;
        private int currentTurn;
        private System.Random rng;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            rng = new System.Random();
            StartCoroutine(WaitForClientsAndSetup());
        }

        private IEnumerator WaitForClientsAndSetup()
        {
            yield return null;

            int maxWaitFrames = 60;
            int waitFrames = 0;
            while (NetworkManager.Singleton.ConnectedClientsIds.Count < 2 && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2)
            {
                Debug.LogWarning($"[MatchController] Only {NetworkManager.Singleton.ConnectedClientsIds.Count} player(s) connected. Expected 2.");
            }

            SetupPlayers();
            matchCoroutine = StartCoroutine(MatchLoop());
        }

        public override void OnNetworkDespawn()
        {
            if (matchCoroutine != null)
            {
                StopCoroutine(matchCoroutine);
            }
            base.OnNetworkDespawn();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitCardsServerRpc(int[] cardIds, ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = serverRpcParams.Receive.SenderClientId;
            if (!playerStates.TryGetValue(clientId, out var playerState)) return;

            cardIds ??= System.Array.Empty<int>();

            if (!CanPlayCards(playerState, cardIds, out int cost))
            {
                RejectSubmission(clientId, "Invalid cards or insufficient energy");
                return;
            }

            turnSubmissions[clientId] = new TurnSubmission(cardIds, true);
            GameEventHub.RaiseCardPlayed(clientId, cardIds);
            AcceptSubmission(clientId);
        }

        private void SetupPlayers()
        {
            if (CardDatabase.Instance == null)
            {
                Debug.LogError("[MatchController] CardDatabase missing");
                return;
            }

            playerStates.Clear();
            playerOrder.Clear();

            var deck = CardDatabase.Instance.BuildDefaultDeck(deckSize);
            var connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);

            Debug.Log($"[MatchController] Setting up {connectedClients.Count} players");

            foreach (ulong clientId in connectedClients)
            {
                var state = new PlayerMatchState(clientId, deck, rng);
                state.ResetForNewMatch();
                playerStates[clientId] = state;
                playerOrder.Add(clientId);
                Debug.Log($"[MatchController] Initialized player {clientId}");
            }

            foreach (var state in playerStates.Values)
            {
                state.GainEnergy(1, maxEnergy);
                state.DrawCards(startingHandSize);
                Debug.Log($"[MatchController] Player {state.ClientId} - Energy: {state.Energy}, Hand: {state.Hand.Count} cards");
                SyncPlayerState(state);
            }
        }

        private IEnumerator MatchLoop()
        {
            currentTurn = 0;

            while (currentTurn < totalTurns)
            {
                currentTurn++;
                turnSubmissions.Clear();

                GameEventHub.RaiseTurnStarted(currentTurn);
                TurnStartedClientRpc(currentTurn);

                foreach (var state in playerStates.Values)
                {
                    state.GainEnergy(1, maxEnergy);
                    state.DrawCards(1);
                    SyncPlayerState(state);
                }

                yield return WaitForSubmissions();

                ResolveTurn();
                GameEventHub.RaiseTurnResolved(currentTurn);
            }

            EndMatch();
        }

        private IEnumerator WaitForSubmissions()
        {
            turnTimer = turnDurationSeconds;

            while (turnTimer > 0f)
            {
                if (turnSubmissions.Count >= playerStates.Count)
                    break;

                turnTimer -= Time.deltaTime;
                yield return null;
            }

            FillMissingSubmissions();
        }

        private void FillMissingSubmissions()
        {
            foreach (var kvp in playerStates)
            {
                if (!turnSubmissions.ContainsKey(kvp.Key))
                {
                    turnSubmissions[kvp.Key] = new TurnSubmission(System.Array.Empty<int>(), false);
                }
            }
        }

        private void ResolveTurn()
        {
            if (playerOrder.Count != 2)
            {
                Debug.LogWarning("[MatchController] Only 2 players supported");
                return;
            }

            var snapshots = ProcessSubmissions();
            ApplyCombat(snapshots);
            SendTurnResults(snapshots);
        }

        private Dictionary<ulong, TurnSnapshot> ProcessSubmissions()
        {
            var snapshots = new Dictionary<ulong, TurnSnapshot>();

            foreach (ulong playerId in playerOrder)
            {
                var snapshot = ProcessPlayerTurn(playerId);
                snapshots[playerId] = snapshot;
            }

            return snapshots;
        }

        // Processes a player's turn: removes played cards, resolves abilities, updates hand
        private TurnSnapshot ProcessPlayerTurn(ulong playerId)
        {
            var player = playerStates[playerId];
            var opponent = playerStates[GetOpponent(playerId)];
            var submission = GetSubmission(playerId);
            var cards = GetCards(submission.CardIds);
            bool handChanged = false;

            if (submission.CardIds.Count > 0)
            {
                int cost = GetTotalCost(cards);
                player.SpendEnergy(cost);
                player.RemoveCardsFromHand(submission.CardIds);
                handChanged = true;
            }

            var abilities = CardAbilityResolver.ResolveAbilities(player, opponent, cards);

            if (abilities.ExtraCardsToDraw > 0)
            {
                player.DrawCards(abilities.ExtraCardsToDraw);
                handChanged = true;
            }

            if (handChanged)
            {
                PushHandUpdate(player);
            }

            return new TurnSnapshot
            {
                PlayerId = playerId,
                Cards = submission.CardIds.ToArray(),
                BasePower = GetTotalPower(cards),
                Abilities = abilities
            };
        }

        private void ApplyCombat(Dictionary<ulong, TurnSnapshot> snapshots)
        {
            foreach (ulong playerId in playerOrder)
            {
                var snapshot = snapshots[playerId];
                var opponentSnapshot = snapshots[GetOpponent(playerId)];

                int power = CalculatePower(snapshot, opponentSnapshot);
                playerStates[playerId].AddScore(power);

                snapshot.FinalPower = power;
                snapshot.FinalScore = playerStates[playerId].Score;
                snapshots[playerId] = snapshot;

                PushScoreUpdate(playerStates[playerId]);
            }
        }

        // Calculates final power after multipliers and blocking effects
        private int CalculatePower(TurnSnapshot player, TurnSnapshot opponent)
        {
            int power = player.BasePower * Mathf.Max(1, player.Abilities.PowerMultiplier);

            if (opponent.Abilities.ShouldBlockOpponent)
            {
                power = 0;
            }

            return power;
        }

        private ulong GetOpponent(ulong playerId)
        {
            int index = playerOrder.IndexOf(playerId);
            return playerOrder[(index + 1) % playerOrder.Count];
        }

        private TurnSubmission GetSubmission(ulong playerId)
        {
            return turnSubmissions.TryGetValue(playerId, out var sub)
                ? sub
                : new TurnSubmission(System.Array.Empty<int>(), false);
        }

        private List<Card> GetCards(IReadOnlyList<int> cardIds)
        {
            var cards = new List<Card>();
            if (cardIds == null) return cards;

            foreach (int id in cardIds)
            {
                if (CardDatabase.Instance.TryGetCard(id, out var card))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        private int GetTotalCost(IReadOnlyList<Card> cards)
        {
            int total = 0;
            foreach (var card in cards)
            {
                total += Mathf.Max(0, card.cost);
            }
            return total;
        }

        private int GetTotalPower(IReadOnlyList<Card> cards)
        {
            int total = 0;
            foreach (var card in cards)
            {
                total += Mathf.Max(0, card.power);
            }
            return total;
        }

        private bool CanPlayCards(PlayerMatchState state, IReadOnlyList<int> cardIds, out int totalCost)
        {
            totalCost = 0;

            if (cardIds == null || cardIds.Count == 0)
                return true;

            if (!state.HandContainsAll(cardIds))
                return false;

            foreach (int id in cardIds)
            {
                if (!CardDatabase.Instance.TryGetCard(id, out var card))
                    return false;

                totalCost += Mathf.Max(0, card.cost);
            }

            return totalCost <= state.Energy;
        }

        private void EndMatch()
        {
            if (playerOrder.Count == 0) return;

            ulong winner = FindWinner();
            int winnerScore = playerStates[winner].Score;
            int loserScore = playerStates[GetOpponent(winner)].Score;

            GameEventHub.RaiseMatchEnded(new MatchResult(winner, winnerScore, loserScore));
            MatchEndedClientRpc(winner, winnerScore, loserScore);
        }

        private ulong FindWinner()
        {
            ulong winner = playerOrder[0];
            int highest = playerStates[winner].Score;

            foreach (ulong id in playerOrder)
            {
                int score = playerStates[id].Score;
                if (score > highest)
                {
                    winner = id;
                    highest = score;
                }
            }

            return winner;
        }

        private void SyncPlayerState(PlayerMatchState state)
        {
            PushHandUpdate(state);
            PushEnergyUpdate(state);
            PushScoreUpdate(state);
        }

        private void AcceptSubmission(ulong clientId)
        {
            SubmissionAcceptedClientRpc(currentTurn, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });
        }

        private void RejectSubmission(ulong clientId, string reason)
        {
            SubmissionRejectedClientRpc(reason, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });
        }

        private void SendTurnResults(Dictionary<ulong, TurnSnapshot> snapshots)
        {
            var ids = new ulong[snapshots.Count];
            var power = new int[snapshots.Count];
            var scores = new int[snapshots.Count];

            int i = 0;
            foreach (var kvp in snapshots)
            {
                ids[i] = kvp.Key;
                power[i] = kvp.Value.FinalPower;
                scores[i] = kvp.Value.FinalScore;
                i++;
            }

            TurnResolvedClientRpc(currentTurn, ids, power, scores);
        }

        private void PushHandUpdate(PlayerMatchState state)
        {
            int[] handArray = state.Hand.ToArray();
            Debug.Log($"[MatchController] Sending hand update to client {state.ClientId}: {handArray.Length} cards");
            HandUpdatedClientRpc(handArray, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { state.ClientId } }
            });
        }

        private void PushEnergyUpdate(PlayerMatchState state)
        {
            EnergyUpdatedClientRpc(state.Energy, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { state.ClientId } }
            });
        }

        private void PushScoreUpdate(PlayerMatchState state)
        {
            ScoreUpdatedClientRpc(state.Score, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { state.ClientId } }
            });
        }

        [ClientRpc]
        private void SubmissionAcceptedClientRpc(int turnNumber, ClientRpcParams rpcParams = default)
        {
            CardGame.UI.GameUI.Instance?.OnSubmissionAccepted();
            Debug.Log($"[MatchController] Submission accepted for turn {turnNumber}");
        }

        [ClientRpc]
        private void SubmissionRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
        {
            CardGame.UI.GameUI.Instance?.OnSubmissionRejected(reason);
            Debug.LogWarning($"[MatchController] Submission rejected: {reason}");
        }

        [ClientRpc]
        private void TurnStartedClientRpc(int turnNumber)
        {
            CardGame.UI.GameUI.Instance?.OnTurnStarted(turnNumber);
            GameEventHub.RaiseTurnStarted(turnNumber);
            Debug.Log($"[MatchController] Turn {turnNumber} started");
        }

        [ClientRpc]
        private void TurnResolvedClientRpc(int turnNumber, ulong[] ids, int[] powerApplied, int[] scores)
        {
            CardGame.UI.GameUI.Instance?.OnTurnResolved(turnNumber, ids, powerApplied, scores);
            GameEventHub.RaiseTurnResolved(turnNumber);
            Debug.Log($"[MatchController] Turn {turnNumber} resolved");
        }

        [ClientRpc]
        private void MatchEndedClientRpc(ulong winnerId, int winnerScore, int loserScore)
        {
            if (GameUI.Instance != null)
            {
                bool isWinner = NetworkManager.Singleton != null &&
                               NetworkManager.Singleton.LocalClientId == winnerId;
                GameUI.Instance.ShowMatchEnd(isWinner, winnerScore, loserScore);
            }
            GameEventHub.RaiseMatchEnded(new MatchResult(winnerId, winnerScore, loserScore));
            Debug.Log($"[MatchController] Match ended. Winner {winnerId} ({winnerScore} - {loserScore})");
        }

        [ClientRpc]
        private void HandUpdatedClientRpc(int[] cardIds, ClientRpcParams rpcParams = default)
        {
            GameUI.Instance?.OnHandUpdated(cardIds);
            Debug.Log($"[MatchController] Hand updated ({cardIds.Length} cards)");
        }

        [ClientRpc]
        private void EnergyUpdatedClientRpc(int energy, ClientRpcParams rpcParams = default)
        {
            GameUI.Instance?.OnEnergyUpdated(energy);
            Debug.Log($"[MatchController] Energy updated: {energy}");
        }

        [ClientRpc]
        private void ScoreUpdatedClientRpc(int score, ClientRpcParams rpcParams = default)
        {
            GameUI.Instance?.OnScoreUpdated(score);
            Debug.Log($"[MatchController] Score updated: {score}");
        }

        private struct TurnSnapshot
        {
            public ulong PlayerId;
            public int[] Cards;
            public int BasePower;
            public AbilityResolutionResult Abilities;
            public int FinalPower;
            public int FinalScore;
        }
    }
}
