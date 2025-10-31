using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using EthicsHotline.Services.Otp;
using EthicsHotline.Services.Sms;
using EthicsHotline.Services.Email;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages
builder.Services.AddRazorPages();

// Rate limit (/otp/send)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("otp-send", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
});

// Config
var cfg = builder.Configuration;
var useRedis = cfg.GetValue<bool>("UseRedis");

// OTP Store
if (useRedis)
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(cfg.GetSection("Redis")["ConnectionString"]!));
    builder.Services.AddSingleton<IOtpStore, RedisOtpStore>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IOtpStore, MemoryOtpStore>();
}

// OTP Options + Service
builder.Services.AddSingleton(new OtpOptions
{
    Digits = cfg.GetValue("Otp:Digits", 6),
    Ttl = TimeSpan.FromMinutes(cfg.GetValue("Otp:TtlMinutes", 5)),
    ResendCooldown = TimeSpan.FromSeconds(cfg.GetValue("Otp:ResendCooldownSeconds", 30)),
    MaxSendPerHour = cfg.GetValue("Otp:MaxSendPerHour", 5),
    MaxVerifyAttempts = cfg.GetValue("Otp:MaxVerifyAttempts", 5)
});
builder.Services.AddScoped<OtpService>();

// SMS
if (string.Equals(cfg["Sms:Provider"], "NetGSM", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ISmsSender, NetGsmSender>(client =>
    {
        client.BaseAddress = new Uri(cfg["Sms:NetGsm:BaseUrl"]!);
    });
}
else
{
    builder.Services.AddSingleton<ISmsSender, MockSmsSender>();
}

// Email
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.MapRazorPages();

// === Endpoints ===

// OTP gönder
app.MapPost("/otp/send", async (SendOtpRequest req,
                                OtpService svc,
                                IEmailSender mailer,
                                IConfiguration cfg,
                                HttpContext ctx) =>
{
    // Kodu üret + SMS'e yolla (Mock ise Console'a düþer)
    var otp = await svc.SendAsync(req.Phone, ctx.Connection.RemoteIpAddress?.ToString());

    // TEST modu: ayný kodu e-posta ile de gönder
    var mirror = cfg.GetValue("Otp:MirrorToEmail", false);
    if (mirror)
    {
        var to = cfg["Otp:MirrorToEmailTo"] ?? cfg["Mail:To"] ?? cfg["Mail:From"];
        var company = cfg["Brand:Company"] ?? "Etik Hat";
        var ttlMin = cfg.GetValue("Otp:TtlMinutes", 5);

        var body = $@"
            <div style='font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial'>
              <h3 style='margin:0 0 10px'>{company} | OTP Test Kodu</h3>
              <p><b>Kod:</b> <span style='font-size:18px'>{otp}</span></p>
              <p><b>Telefon:</b> {System.Net.WebUtility.HtmlEncode(req.Phone)}</p>
              <p style='color:#64748b'>Bu kod {ttlMin} dakika içinde geçerlidir. Test amaçlý gönderilmiþtir.</p>
            </div>";
        await mailer.SendAsync(to!, $"{company} | OTP Test Kodu", body, isHtml: true);
    }

    var msg = mirror ? "Kod gönderildi (test: e-posta ile de iletildi)." : "Kod gönderildi.";
    return Results.Ok(new { message = msg });
}).RequireRateLimiting("otp-send");


// OTP doðrula
app.MapPost("/otp/verify", async (VerifyOtpRequest req, OtpService svc) =>
{
    var ok = await svc.VerifyAsync(req.Phone, req.Code, consumeIfValid: false);
    return ok ? Results.Ok(new { success = true })
              : Results.BadRequest(new { success = false, message = "Kod hatalý veya süresi doldu." });
});

// Form gönder (OTP opsiyonel / feature flag ile)
app.MapPost("/form/submit", async (SubmitFormRequest req, OtpService svc, IEmailSender mailer, IConfiguration conf) =>
{
    var requireOtp = conf.GetValue("Features:RequireOtp", true);

    if (requireOtp)
    {
        if (string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.OtpCode))
            return Results.BadRequest(new { message = "Doðrulama gerekli." });

        var verified = await svc.VerifyAsync(req.Phone, req.OtpCode, consumeIfValid: true);
        if (!verified)
            return Results.BadRequest(new { message = "Doðrulama gerekli." });
    }
    else
    {
        if (!string.IsNullOrWhiteSpace(req.Phone) && !string.IsNullOrWhiteSpace(req.OtpCode))
            await svc.VerifyAsync(req.Phone, req.OtpCode, consumeIfValid: true);
    }

    // Mail gövdesi (markalý)
    static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
    var phoneRow = string.IsNullOrWhiteSpace(req.Phone) ? "" : $"<p><b>Telefon:</b> {Html(req.Phone)}</p>";

    var company = conf["Brand:Company"] ?? "Etik Hat";
    var logoUrl = conf["Brand:LogoUrl"];
    var primary = conf["Brand:Primary"] ?? "#1593d4";

    var headerHtml = !string.IsNullOrWhiteSpace(logoUrl)
        ? $"<div style='padding:12px 0 8px'><img src=\"{logoUrl}\" alt=\"{company}\" style=\"height:48px;max-width:100%\"/></div>" +
          $"<hr style='border:none;border-top:2px solid {primary};opacity:.6;margin:6px 0 14px'/>"
        : $"<h2 style='margin:0 0 8px;color:#111'>{company}</h2>" +
          $"<hr style='border:none;border-top:2px solid {primary};opacity:.6;margin:6px 0 14px'/>";

    var body = $@"
      <div style='font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial'>
        {headerHtml}
        <h3 style='margin:0 0 10px'>Yeni Etik Hat Bildirimi</h3>
        <p><b>Kategori:</b> {Html(req.Category)}</p>
        <p><b>Tarih/Saat:</b> {req.EventDate:dd.MM.yyyy} {req.EventTime}</p>
        <p><b>Konum:</b> {Html(req.Location)}</p>
        <p><b>Detay:</b><br/>{Html(req.Details)}</p>
        <p><b>Ýlgili Kiþiler:</b> {Html(req.People)}</p>
        {phoneRow}
        <p><b>KVKK Onayý:</b> {(req.KvkkConsent ? "Evet" : "Hayýr")}</p>
      </div>";

    var to = conf["Mail:To"] ?? conf["Mail:From"]!;
    await mailer.SendAsync(to, $"{company} | Etik Hat Bildirimi", body, isHtml: true);

    return Results.Ok(new { message = "Bildiriminiz iletilmiþtir." });
});

// UI'nýn RequireOtp flag'ini okumasý için
app.MapGet("/features", (IConfiguration conf) =>
{
    var requireOtp = conf.GetValue("Features:RequireOtp", true);
    return Results.Ok(new { requireOtp });
});

app.Run();

// === Request records ===
public record SendOtpRequest(string Phone);
public record VerifyOtpRequest(string Phone, string Code);
public record SubmitFormRequest(
    string Category,
    DateTime EventDate,
    TimeSpan EventTime,
    string? Location,
    string Details,
    string? People,
    string Phone,
    string OtpCode,
    bool KvkkConsent
);
