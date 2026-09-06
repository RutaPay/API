using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Models
{
    public class UserReward
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ForeignKey("User")]
        public string UserID { get; set; } = string.Empty;
        public User? User { get; set; }

        [Required]
        [ForeignKey("Reward")]
        public int RewardId { get; set; }
        public Reward? Reward { get; set; }

        public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        public bool IsUsed { get; set; } = false;
    }
}
