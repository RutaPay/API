namespace Api.Dtos.Transaction
{
    public class TransactionDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty; // Recharge, TripPayment, RewardRedemption, Refund
        public decimal Amount { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RouteName { get; set; }
        public string? BusUnitId { get; set; }
        public string? PaymentMethod { get; set; }
        public string Reference { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class RechargeRequestDto
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Card"; // Card, SPEI, OXXO
        public string? CardNumberMasked { get; set; }
    }
}
