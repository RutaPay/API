using Api.Interfaces;
using Api.Models;
using Microsoft.Extensions.Options;
using RestSharp;
using RestSharp.Authenticators;

namespace Api.Service
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task<RestResponse> SendVerificationEmailAsync(string email, string verificationToken)
        {
            /*var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Verifica tu cuenta";

            var verificationUrl = $"/auth/verify?token={verificationToken}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <h2>¡Bienvenido a nuestra aplicación!</h2>
                <p>Por favor, haz clic en el siguiente enlace para verificar tu correo electrónico:</p>
                <a href='{verificationUrl}' style='padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Verificar cuenta</a>
                <p>Si tú no solicitaste esto, puedes ignorar este mensaje.</p>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_emailSettings.Server, _emailSettings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                await client.SendAsync(message);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se pudo enviar el correo: {ex.Message}");
            }
            finally
            {
                await client.DisconnectAsync(true);
            }*/
            var apiKey = !string.IsNullOrWhiteSpace(_emailSettings.ApiKey)
                ? _emailSettings.ApiKey
                : Environment.GetEnvironmentVariable("API_KEY") ?? "";

            var domain = !string.IsNullOrWhiteSpace(_emailSettings.Domain)
                ? _emailSettings.Domain
                : "rutapay.de5.net";

            var baseUrl = !string.IsNullOrWhiteSpace(_emailSettings.BaseUrl)
                ? _emailSettings.BaseUrl
                : "https://api.mailgun.net";

            var options = new RestClientOptions(baseUrl)
            {
                Authenticator = new HttpBasicAuthenticator("api", apiKey)
            };

            var client = new RestClient(options);
            var request = new RestRequest($"/v3/{domain}/messages", Method.Post);
            request.AlwaysMultipartFormData = true;
            request.AddParameter("from", "Mailgun Sandbox <postmaster@rutapay.de5.net>");
            request.AddParameter("to", "RutaPay <rutapaymx@gmail.com>");
            request.AddParameter("subject", "Hello RutaPay");
            request.AddParameter("text", "Congratulations RutaPay, you just sent an email with Mailgun! You are truly awesome!");
            return await client.ExecuteAsync(request);
        }
    }
}
