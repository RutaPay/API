namespace Api.Dtos.Payment
{
    /// <summary>
    /// Terminal -> API. La terminal a bordo solicita su token de sesión firmado
    /// (el que su frontend mostrará en el QR y emitirá por NFC).
    /// </summary>
    public class TerminalSessionRequestDto
    {
        public string TerminalCode { get; set; } = string.Empty;

        /// <summary>Credencial estática de la terminal (su "token de pago").</summary>
        public string Credential { get; set; } = string.Empty;
    }

    /// <summary>
    /// API -> Terminal. Token de sesión de corta duración con el que el frontend
    /// de la terminal arma el QR/NFC del viaje.
    /// </summary>
    public class TerminalSessionResponseDto
    {
        public string TerminalCode { get; set; } = string.Empty;
        public string BusUnitId { get; set; } = string.Empty;
        public string? Route { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
        public DateTime IssuedAt { get; set; }
    }

    /// <summary>
    /// App del cliente -> API. El pasajero autenticado escanea el QR/NFC de la
    /// terminal y autoriza el cobro con su propia identidad.
    /// </summary>
    public class AuthorizePaymentRequestDto
    {
        /// <summary>Token de sesión de la terminal leído del QR o del NFC.</summary>
        public string TerminalToken { get; set; } = string.Empty;

        /// <summary>Medio usado por el cliente: QR_Terminal | NFC_Terminal.</summary>
        public string? PaymentMethod { get; set; }
    }

    /// <summary>
    /// Datos de la terminal expuestos al panel de administración.
    /// </summary>
    public class TerminalResponseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string BusUnitId { get; set; } = string.Empty;
        public string? RouteName { get; set; }
        public string State { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Alta de una terminal. Devuelve la credencial en texto claro UNA sola vez.
    /// </summary>
    public class CreateTerminalRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public string BusUnitId { get; set; } = string.Empty;
        public string? RouteName { get; set; }
    }

    /// <summary>Cambio de estado de una terminal.</summary>
    public class SetTerminalStateRequestDto
    {
        // Active | Inactive | Maintenance
        public string State { get; set; } = string.Empty;
    }
}
