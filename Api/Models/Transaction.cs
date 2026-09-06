using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Models
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ForeignKey("User")]
        public string UserID { get; set; } = string.Empty;
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string CardUID { get; set; } = string.Empty;

        // Recharge, TripPayment, RewardRedemption, Refund
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Amount { get; set; }

        [Precision(18, 2)]
        public decimal PreviousBalance { get; set; }

        [Precision(18, 2)]
        public decimal CurrentBalance { get; set; }

        // Completed, Pending, Failed
        [MaxLength(30)]
        public string Status { get; set; } = "Completed";

        [MaxLength(100)]
        public string? RouteName { get; set; }

        [MaxLength(50)]
        public string? BusUnitId { get; set; }

        // Card, SPEI, OXXO, QR_Pass, Points_Redeem
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
