using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Models
{
    public class VerificationRequest
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ForeignKey("User")]
        public string UserID { get; set; } = string.Empty;
        public User? User { get; set; }

        // Student, Health, Adult
        [Required]
        [MaxLength(50)]
        public string TargetRole { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? CurpDocumentUrl { get; set; }

        [MaxLength(255)]
        public string? CredentialDocumentUrl { get; set; }

        // Pending, Approved, Rejected
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        [MaxLength(500)]
        public string? ReviewNotes { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(450)]
        public string? ReviewedByAdminId { get; set; }
    }
}
