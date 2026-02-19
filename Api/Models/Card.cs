using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Models
{
    public class Card
    {
        // ID del Usuario que es dueño de la tarjeta (si está asociada a una cuenta)
        [Key]
        [ForeignKey("User")]
        public int? UserID { get; set; }
        public User? User { get; set; }

        // El número o UID que se usa al tocar el validador (NFC/QR)
        public required string UID { get; set; }

        // El saldo actual de la tarjeta, en la unidad monetaria
        [Precision(18, 2)]
        public decimal Balance { get; set; }

        // Estado: Activa, Inactiva, Bloqueada...
        public string State { get; set; } = "Active";
    }
}
