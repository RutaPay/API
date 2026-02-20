namespace Api.Dtos.User
{
    public class UserDto
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string LastNames { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
