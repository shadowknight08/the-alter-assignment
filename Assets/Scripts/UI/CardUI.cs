using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGame.Gameplay.Cards;

namespace CardGame.UI
{
    // Displays a single card in the player's hand
    public class CardUI : MonoBehaviour
    {
        [Header("Card Display")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI powerText;
        [SerializeField] private TextMeshProUGUI abilitiesText;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image selectedIndicator;

        private Card cardData;
        private GameUI gameUI;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button == null)
                button = gameObject.AddComponent<Button>();

            button.onClick.AddListener(OnClicked);

            if (selectedIndicator != null)
                selectedIndicator.gameObject.SetActive(false);
        }

        public void Initialize(Card card, GameUI ui)
        {
            cardData = card;
            gameUI = ui;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (cardData == null) return;

            if (nameText != null)
                nameText.text = cardData.name;

            if (costText != null)
                costText.text = $"Cost: {cardData.cost}";

            if (powerText != null)
                powerText.text = $"Power: {cardData.power}";

            if (abilitiesText != null)
            {
                abilitiesText.text = cardData.abilities != null && cardData.abilities.Length > 0
                    ? string.Join(", ", cardData.abilities)
                    : "";
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null)
                selectedIndicator.gameObject.SetActive(selected);

            if (cardImage != null)
            {
                Color color = selected ? Color.yellow : Color.white;
                color.a = cardImage.color.a;
                cardImage.color = color;
            }
        }

        private void OnClicked()
        {
            if (gameUI != null && cardData != null)
                gameUI.OnCardClicked(cardData.id);
        }
    }
}
