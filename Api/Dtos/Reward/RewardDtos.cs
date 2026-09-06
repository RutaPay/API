namespace Api.Dtos.Reward
{
    public class RewardDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int PointsCost { get; set; }
        public string RewardType { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public bool IsActive { get; set; }
    }

    public class RedeemRewardDto
    {
        public int RewardId { get; set; }
    }

    public class UserRewardDto
    {
        public Guid Id { get; set; }
        public int RewardId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime RedeemedAt { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
    }
}
