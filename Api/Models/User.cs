using Microsoft.AspNetCore.Identity;

namespace Api.Models
{
    public class User : IdentityUser
    {
        // Identificador único para la base de datos (clave primaria)
        public int Id { get; set; }

        // Nombre completo del usuario
        public string FullName { get; set; } = String.Empty;

        // Apellidos del usuario
        public string LastNames { get; set; } = String.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public Card? Card { get; set; }
        public Point? Point { get; set; }
    }
}
