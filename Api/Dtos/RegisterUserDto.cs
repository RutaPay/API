namespace Api.Dtos
{
    public class RegisterUserDto
    {

        public required string FullName { get; set; }

        public required string LastNames { get; set; }

        public required string Email { get; set; }

        public required string PhoneNumber { get; set; }

        public required string Password { get; set; }

    }
}
