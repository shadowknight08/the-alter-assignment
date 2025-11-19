using System.Collections.Generic;
using CardGame.Gameplay.Cards;

namespace CardGame.Gameplay.Match
{
    public static class CardAbilityResolver
    {
        // Processes all card abilities and applies their effects (score changes, power multipliers, etc.)
        public static AbilityResolutionResult ResolveAbilities(
            PlayerMatchState actingPlayer,
            PlayerMatchState opponent,
            IReadOnlyList<Card> playedCards)
        {
            var result = AbilityResolutionResult.Default;

            if (playedCards == null || playedCards.Count == 0)
            {
                return result;
            }

            foreach (var card in playedCards)
            {
                if (card.abilities == null || card.abilities.Length == 0)
                {
                    continue;
                }

                foreach (string abilityName in card.abilities)
                {
                    if (!System.Enum.TryParse<CardAbilityType>(abilityName, out var ability))
                    {
                        continue;
                    }

                    switch (ability)
                    {
                        case CardAbilityType.GainPoints:
                            actingPlayer.AddScore(2);
                            break;
                        case CardAbilityType.StealPoints:
                            if (opponent.Score > 0)
                            {
                                opponent.AddScore(-1);
                                actingPlayer.AddScore(1);
                            }
                            break;
                        case CardAbilityType.BlockNextAttack:
                            result.ShouldBlockOpponent = true;
                            break;
                        case CardAbilityType.DoublePower:
                            result.PowerMultiplier *= 2;
                            break;
                        case CardAbilityType.DrawExtraCard:
                            result.ExtraCardsToDraw++;
                            break;
                    }

                    GameEventHub.RaiseAbilityTriggered(
                        new AbilityTriggerContext(actingPlayer.ClientId, opponent.ClientId, abilityName, card.id));
                }
            }

            return result;
        }
    }

    public struct AbilityResolutionResult
    {
        public int PowerMultiplier;
        public bool ShouldBlockOpponent;
        public int ExtraCardsToDraw;

        public static AbilityResolutionResult Default => new AbilityResolutionResult
        {
            PowerMultiplier = 1,
            ShouldBlockOpponent = false,
            ExtraCardsToDraw = 0
        };
    }
}

