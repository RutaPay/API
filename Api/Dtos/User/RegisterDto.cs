using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.User
{
    public class RegisterDto
    {
        [Required]
        public string? FullName { get; set; }

        [Required]
        public string? LastNames { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? Password { get; set; }

        [Required]
        [Phone]
        public string? PhoneNumber { get; set; }
    }
}
