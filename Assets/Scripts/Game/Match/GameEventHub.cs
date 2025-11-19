using System;
using System.Collections.Generic;

namespace CardGame.Gameplay.Match
{
    public static class GameEventHub
    {
        public static event Action<int> OnTurnStarted;
        public static event Action<int> OnTurnResolved;
        public static event Action<ulong, IReadOnlyList<int>> OnCardPlayed;
        public static event Action<AbilityTriggerContext> OnAbilityTriggered;
        public static event Action<MatchResult> OnMatchEnded;

        public static void RaiseTurnStarted(int turn) => OnTurnStarted?.Invoke(turn);
        public static void RaiseTurnResolved(int turn) => OnTurnResolved?.Invoke(turn);
        public static void RaiseCardPlayed(ulong clientId, IReadOnlyList<int> cardIds) => OnCardPlayed?.Invoke(clientId, cardIds);
        public static void RaiseAbilityTriggered(AbilityTriggerContext ctx) => OnAbilityTriggered?.Invoke(ctx);
        public static void RaiseMatchEnded(MatchResult result) => OnMatchEnded?.Invoke(result);
    }

    public readonly struct AbilityTriggerContext
    {
        public readonly ulong ActingPlayerId;
        public readonly ulong TargetPlayerId;
        public readonly string AbilityName;
        public readonly int CardId;

        public AbilityTriggerContext(ulong actingPlayerId, ulong targetPlayerId, string abilityName, int cardId)
        {
            ActingPlayerId = actingPlayerId;
            TargetPlayerId = targetPlayerId;
            AbilityName = abilityName;
            CardId = cardId;
        }
    }

    public readonly struct MatchResult
    {
        public readonly ulong WinnerId;
        public readonly int WinnerScore;
        public readonly int LoserScore;

        public MatchResult(ulong winnerId, int winnerScore, int loserScore)
        {
            WinnerId = winnerId;
            WinnerScore = winnerScore;
            LoserScore = loserScore;
        }
    }
}


