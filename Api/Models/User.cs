namespace Api.Models
{
    public class User
    {
        // Identificador único para la base de datos (clave primaria)
        public int Id { get; set; }

        // Nombre completo del usuario
        public string FullName { get; set; }

        // Apellidos del usuario
        public string LastNames { get; set; }

        // Correo electrónico del usuario
        public string Email { get; set; }

        // Número de teléfono del usuario
        public string PhoneNumber { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
