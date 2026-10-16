using System.ComponentModel.DataAnnotations;

namespace Api.Models
{
    /// <summary>
    /// Terminal validadora instalada a bordo de una unidad de transporte.
    /// Cada terminal posee su propia credencial de pago (token estático) con la que
    /// su frontend solicita un token de sesión firmado para generar el QR y el NFC
    /// de cada viaje.
    /// </summary>
    public class Terminal
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Identificador legible de la terminal física (ej. "VAL-NFC-04").
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        // Unidad de transporte donde está instalada la terminal (ej. "UNIDAD-CEL-01").
        [Required]
        [MaxLength(50)]
        public string BusUnitId { get; set; } = string.Empty;

        // Ruta que cubre la unidad de transporte de la terminal.
        [MaxLength(120)]
        public string? RouteName { get; set; }

        // Estado de la terminal: Active, Inactive, Maintenance.
        [Required]
        [MaxLength(20)]
        public string State { get; set; } = "Active";

        // Credencial estática de la terminal (se guarda hasheada con sal).
        [Required]
        [MaxLength(200)]
        public string CredentialHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CredentialSalt { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Verifica la credencial de pago de la terminal en tiempo constante.
        /// </summary>
        public bool VerifyCredential(string credential)
        {
            if (string.IsNullOrEmpty(credential) || string.IsNullOrEmpty(CredentialHash))
            {
                return false;
            }

            var candidate = Service.HashService.Hash(credential, CredentialSalt);
            var candidateBytes = System.Text.Encoding.UTF8.GetBytes(candidate);
            var storedBytes = System.Text.Encoding.UTF8.GetBytes(CredentialHash);

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                candidateBytes, storedBytes);
        }
    }
}
