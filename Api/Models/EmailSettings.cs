namespace Api.Models
{
    public class EmailSettings
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string SenderName { get; set; }
        public string SenderEmail { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Propiedades para Mailgun API
        public string ApiKey { get; set; } = string.Empty;
        public string Domain { get; set; } = "rutapay.de5.net";
        public string BaseUrl { get; set; } = "https://api.mailgun.net";
    }
}
