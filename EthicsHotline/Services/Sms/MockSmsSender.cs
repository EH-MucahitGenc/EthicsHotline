namespace EthicsHotline.Services.Sms;

public sealed class MockSmsSender : ISmsSender
{
    public Task SendAsync(string phoneE164, string message, CancellationToken ct = default)
    {
        Console.WriteLine($"[MOCK SMS] {phoneE164} => {message}");
        return Task.CompletedTask;
    }
}
