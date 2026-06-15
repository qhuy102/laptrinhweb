using System.Threading.Tasks;

namespace Web_Quản_Lí_Nhà_Thuốc.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
