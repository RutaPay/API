namespace Api.Dtos.User
{
    public class UserForAdminDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string FullName { get; set; }
        public string LastNames { get; set; }
        public string AccountType { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public string CardId { get; set; }
        public decimal CardBalance { get; set; }
        public string CardStatus { get; set; }
        public int Points { get; set; }
    }
}
