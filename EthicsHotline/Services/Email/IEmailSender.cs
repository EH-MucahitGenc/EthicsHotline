namespace EthicsHotline.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, bool isHtml = false);
}
