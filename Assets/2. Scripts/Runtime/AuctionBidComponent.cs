using UnityEngine;

namespace CantResell
{
    public sealed class AuctionBidComponent : MonoBehaviour
    {
        [SerializeField, Range(1, 1000)] private int minimumRaise = 10; //최소 입찰 증가액
        public int bidderSlot { get; private set; } = -1; //최고 입찰자의 좌석
        public int highestBid { get; private set; } //최고 입찰 금액
        public int raiseAmount => Mathf.Clamp(minimumRaise, 1, 1000); //검증된 최소 증가액

        public void resetBids() //새 라운드의 입찰 초기화
        {
            bidderSlot = -1;
            highestBid = 0;
        }

        public bool tryBid(int slot, int sellerSlot, int balance, int amount) //판매자 입찰과 잔액 초과 및 낮은 입찰 차단
        {
            if (slot < 0 || slot >= 4 || slot == sellerSlot || amount <= 0 || amount > balance)
                return false;
            if ((long)amount < (long)highestBid + raiseAmount)
                return false;
            bidderSlot = slot;
            highestBid = amount;
            return true;
        }
    }
}
