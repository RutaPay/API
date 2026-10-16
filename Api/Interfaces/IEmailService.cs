using RestSharp;

namespace Api.Interfaces
{
    public interface IEmailService
    {
        Task<RestResponse> SendVerificationEmailAsync(string email, string verificationToken);
    }
}
