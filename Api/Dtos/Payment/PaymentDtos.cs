namespace Api.Dtos.Payment
{
    public class ValidatePaymentRequestDto
    {
        public string Token { get; set; } = string.Empty;
        public string? Route { get; set; }
        public string? BusUnitId { get; set; }
        public string? SrGroupId { get; set; }
    }

    public class GeneratePaymentRequestDto
    {
        public string? Route { get; set; }
        public string? BusUnitId { get; set; }
    }
}
