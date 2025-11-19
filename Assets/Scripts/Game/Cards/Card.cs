using System;

namespace CardGame.Gameplay.Cards
{
    [Serializable]
    public enum CardAbilityType
    {
        GainPoints,
        StealPoints,
        BlockNextAttack,
        DoublePower,
        DrawExtraCard
    }

    [Serializable]
    public class Card
    {
        public int id;
        public string name;
        public int cost;
        public int power;
        public string[] abilities;

        public bool HasAbility(CardAbilityType ability)
        {
            if (abilities == null || abilities.Length == 0)
            {
                return false;
            }

            foreach (string abilityName in abilities)
            {
                if (Enum.TryParse<CardAbilityType>(abilityName, out var parsed) && parsed == ability)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

