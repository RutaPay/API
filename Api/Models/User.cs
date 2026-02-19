namespace Api.Models
{
    public class User
    {
        // Identificador único para la base de datos (clave primaria)
        public int Id { get; set; }

        // Nombre completo del usuario
        public string FullName { get; set; } = String.Empty;

        // Apellidos del usuario
        public string LastNames { get; set; } = String.Empty;

        // Correo electrónico del usuario
        public string Email { get; set; } = String.Empty;

        // Número de teléfono del usuario
        public string PhoneNumber { get; set; } = String.Empty;

        // Contraseña del usuario (almacenada como hash)
        public string PasswordHash { get; set; }

        // Sal para el hash de la contraseña (para mayor seguridad)
        public string Salt { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public Card? Card { get; set; }
        public Point? Point { get; set; }
    }
}
