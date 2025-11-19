using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Gameplay.Cards
{
    // Loads cards from JSON and provides lookup. Persists across scenes.
    // To add cards, just edit the JSON file.
    public class CardDatabase : MonoBehaviour
    {
        private static CardDatabase instance;
        public static CardDatabase Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<CardDatabase>();
                    if (instance == null)
                    {
                        Debug.LogError("[CardDatabase] No CardDatabase found in scene.");
                    }
                }

                return instance;
            }
        }

        [SerializeField] private string resourcePath = "Cards/cards";

        private readonly Dictionary<int, Card> cardsById = new();
        private readonly List<Card> cards = new();
        private bool isLoaded;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCards();
        }

        private void LoadCards()
        {
            if (isLoaded)
            {
                return;
            }

            TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[CardDatabase] Failed to load card data at path: {resourcePath}");
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<CardCollection>(jsonAsset.text);
                cards.Clear();
                cardsById.Clear();

                if (wrapper != null && wrapper.cards != null)
                {
                    foreach (var definition in wrapper.cards)
                    {
                        cards.Add(definition);
                        cardsById[definition.id] = definition;
                    }

                    isLoaded = true;
                    Debug.Log($"[CardDatabase] Loaded {cards.Count} card definitions.");
                }
                else
                {
                    Debug.LogError("[CardDatabase] No card entries found in JSON.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardDatabase] Failed to parse card data: {ex.Message}");
            }
        }

        public bool TryGetCard(int id, out Card definition)
        {
            if (!isLoaded)
            {
                LoadCards();
            }

            return cardsById.TryGetValue(id, out definition);
        }

        public IReadOnlyList<Card> GetAllCards()
        {
            if (!isLoaded)
            {
                LoadCards();
            }

            return cards;
        }

        public List<int> BuildDefaultDeck(int deckSize)
        {
            if (!isLoaded)
            {
                LoadCards();
            }

            var deck = new List<int>(deckSize);
            if (cards.Count == 0)
            {
                return deck;
            }

            int index = 0;
            while (deck.Count < deckSize)
            {
                deck.Add(cards[index % cards.Count].id);
                index++;
            }

            return deck;
        }

        [Serializable]
        private class CardCollection
        {
            public Card[] cards;
        }
    }
}

