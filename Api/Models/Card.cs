namespace Api.Models
{
    public class Card
    {
        // Identificador único para la base de datos (clave primaria)
        public int Id { get; set; }

        // El número o UID que se usa al tocar el validador (NFC/QR)
        public required string UID { get; set; }

        // El saldo actual de la tarjeta, en la unidad monetaria
        public decimal Balance { get; set; }

        // Estado: Activa, Inactiva, Bloqueada...
        public string State { get; set; } = "Active";

        // ID del Usuario que es dueño de la tarjeta (si está asociada a una cuenta)
        public int? UserID { get; set; }
        public User? User { get; set; }
    }
}
