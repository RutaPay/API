using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Models
{
    public class Point
    {
        // ID del Usuario
        [Key]
        [ForeignKey("User")]
        public string UserID { get; set; }
        public User? User { get; set; }

        // Cantidad de puntos acumulados por el usuario
        public int Points { get; set; } = 0;
    }
}
