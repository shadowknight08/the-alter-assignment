using System.Collections.Generic;
using CardGame.Gameplay.Cards;
using UnityEngine;

namespace CardGame.Gameplay.Match
{
    public class PlayerMatchState
    {
        private readonly System.Random rng;

        public ulong ClientId { get; }
        public List<int> Deck { get; } = new();
        public List<int> Hand { get; } = new();
        public int Energy { get; private set; }
        public int Score { get; private set; }

        public PlayerMatchState(ulong clientId, IEnumerable<int> startingDeck, System.Random rng)
        {
            ClientId = clientId;
            this.rng = rng;
            Deck.AddRange(startingDeck);
            ShuffleDeck();
        }

        public void ResetForNewMatch()
        {
            Energy = 0;
            Score = 0;
            Hand.Clear();
        }

        public void GainEnergy(int amount, int maxEnergy)
        {
            Energy = Mathf.Min(maxEnergy, Energy + amount);
        }

        public void SpendEnergy(int amount)
        {
            Energy = Mathf.Max(0, Energy - amount);
        }

        public void AddScore(int delta)
        {
            Score = Mathf.Max(0, Score + delta);
        }

        public List<int> DrawCards(int count)
        {
            var drawn = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (Deck.Count == 0)
                {
                    break;
                }

                int lastIndex = Deck.Count - 1;
                int cardId = Deck[lastIndex];
                Deck.RemoveAt(lastIndex);
                Hand.Add(cardId);
                drawn.Add(cardId);
            }

            return drawn;
        }

        public void ReturnCardsToDeck(IEnumerable<int> cardIds)
        {
            foreach (var cardId in cardIds)
            {
                Deck.Insert(0, cardId);
            }
        }

        public bool RemoveCardsFromHand(IEnumerable<int> cardIds)
        {
            var markedForRemoval = new List<int>(cardIds);
            foreach (int id in markedForRemoval)
            {
                if (!Hand.Remove(id))
                {
                    return false;
                }
            }

            return true;
        }

        // Checks if hand contains all specified cards (uses temp copy to handle duplicates)
        public bool HandContainsAll(IEnumerable<int> cardIds)
        {
            var tempHand = new List<int>(Hand);
            foreach (int id in cardIds)
            {
                if (!tempHand.Remove(id))
                {
                    return false;
                }
            }

            return true;
        }

        private void ShuffleDeck()
        {
            for (int i = Deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
            }
        }
    }
}


