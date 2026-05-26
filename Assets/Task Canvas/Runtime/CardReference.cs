using System;

namespace TaskCanvas
{
    [Serializable]
    public class CardReference
    {
        public string cardId;

        public CardReference() { }

        public CardReference(string cardId)
        {
            this.cardId = cardId;
        }

        public static implicit operator string(CardReference reference) => reference?.cardId;
        public static implicit operator CardReference(string cardId) => new CardReference(cardId);
    }
}
