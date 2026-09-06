namespace Api.Dtos.Card
{
    public class BalanceChangeDto
    {
        public decimal Balance { get; set; }

        public string OppType { get; set; } = "recharge"; // recharge or payment

        public string? PaymentMethod { get; set; } = "Card"; // Card, SPEI, OXXO
    }
}
