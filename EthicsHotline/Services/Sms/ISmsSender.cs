namespace EthicsHotline.Services.Sms;

public interface ISmsSender
{
    Task SendAsync(string phoneE164, string message, CancellationToken ct = default);
}
