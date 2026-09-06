using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Api.Models
{
    public class Reward
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public int PointsCost { get; set; }

        // Balance, Raffle, Discount, TripPass
        [Required]
        [MaxLength(50)]
        public string RewardType { get; set; } = "Balance";

        [Precision(18, 2)]
        public decimal Value { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
