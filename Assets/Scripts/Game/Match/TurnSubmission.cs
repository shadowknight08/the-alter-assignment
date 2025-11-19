using System.Collections.Generic;

namespace CardGame.Gameplay.Match
{
    // Stores which cards a player submitted for a turn
    public class TurnSubmission
    {
        public List<int> CardIds { get; }
        public bool SubmittedByPlayer { get; }

        public TurnSubmission(IEnumerable<int> cardIds, bool submittedByPlayer)
        {
            CardIds = new List<int>(cardIds ?? new List<int>());
            SubmittedByPlayer = submittedByPlayer;
        }
    }
}


