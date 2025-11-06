using EthicsHotline.Services.Email;
using EthicsHotline.Services.Otp;
using EthicsHotline.Services.Sms;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages
builder.Services.AddRazorPages();

// Antiforgery (CSRF)
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

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

// === Config ===
var cfg = builder.Configuration;
var useRedis = cfg.GetValue<bool>("UseRedis", false);

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
    MaxSendPerHour = cfg.GetValue("Otp:MaxSendPerHour", 2), // SAATTE 2
    MaxVerifyAttempts = cfg.GetValue("Otp:MaxVerifyAttempts", 5)
});

builder.Services.AddSingleton<OtpRateLimiter>(sp =>
{
    var cache = sp.GetService<IMemoryCache>();
    var redis = useRedis ? sp.GetService<IConnectionMultiplexer>() : null;
    var opt = sp.GetRequiredService<OtpOptions>();
    return new OtpRateLimiter(opt, cache, redis);
});

builder.Services.AddScoped<OtpService>();

// ---- PoliDijital (tek provider) ----
builder.Services
    .AddOptions<PoliDijitalOptions>()
    .Bind(builder.Configuration.GetSection("Sms:PoliDijital"))
    .Validate(o =>
        !string.IsNullOrWhiteSpace(o.Host) &&
        !string.IsNullOrWhiteSpace(o.Username) &&
        !string.IsNullOrWhiteSpace(o.Password) &&
        (o.Port == 9587 || o.Port == 9588),
        "Sms:PoliDijital yapýlandýrmasý eksik/hatalý (Host, Username, Password, Port 9587/9588).")
    .ValidateOnStart();

builder.Services.AddHttpClient<IPoliDijitalClient, PoliDijitalClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<PoliDijitalOptions>>().Value;
    var scheme = opt.Port == 9588 ? "https" : "http";
    client.BaseAddress = new Uri($"{scheme}://{opt.Host}:{opt.Port}/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ISmsSender -> Poli adapter
builder.Services.AddSingleton<ISmsSender, PoliSmsSenderAdapter>();

// Email
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ---------- MÜÞTERÝ/Oturum Kimliði (cookie) ----------
const string ClientCookie = "eh_client";
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Cookies.ContainsKey(ClientCookie))
    {
        var id = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_'); // URL-safe
        ctx.Response.Cookies.Append(ClientCookie, id, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
    }
    await next();
});
// ------------------------------------------------------

app.UseRateLimiter();

// Exception handler: son kullanýcýya güvenli JSON
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        string msg;
        int status;

        if (ex is InvalidOperationException || ex is ArgumentException)
        {
            msg = ex.Message;
            status = StatusCodes.Status400BadRequest;
        }
        else
        {
            msg = "Beklenmeyen bir hata oluþtu.";
            status = StatusCodes.Status500InternalServerError;
        }

        context.Response.StatusCode = status;
        var payload = System.Text.Json.JsonSerializer.Serialize(new { success = false, message = msg });
        await context.Response.WriteAsync(payload);
    });
});

app.MapRazorPages();

// === Endpoints ===

// OTP gönder
app.MapPost("/otp/send", async (SendOtpRequest req,
                                OtpService svc,
                                IEmailSender mailer,
                                IConfiguration cfg2,
                                HttpContext ctx) =>
{
    var clientId = ctx.Request.Cookies[ClientCookie] ?? "anon";
    var ip = ctx.Connection.RemoteIpAddress?.ToString();

    var mirror = cfg2.GetValue("Otp:MirrorToEmail", false);

    string otp;
    if (mirror)
    {
        // Mirror-to-email: SMS DENEME, sadece üret + store
        otp = await svc.GenerateOnlyAsync(req.Phone, clientId, ip);
    }
    else
    {
        // Normal mod: SMS gönder
        otp = await svc.SendAsync(req.Phone, clientId, ip);
    }

    if (mirror)
    {
        var to = cfg2["Otp:MirrorToEmailTo"] ?? cfg2["Mail:To"] ?? cfg2["Mail:From"];
        var company = cfg2["Brand:Company"] ?? "Etik Hat";
        var ttlMin = cfg2.GetValue("Otp:TtlMinutes", 5);

        var body = $@"
            <div style='font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial'>
              <h3 style='margin:0 0 10px'>{company} | OTP Test Kodu</h3>
              <p><b>Kod:</b> <span style='font-size:18px'>{otp}</span></p>
              <p><b>Telefon:</b> {System.Net.WebUtility.HtmlEncode(req.Phone)}</p>
              <p style='color:#64748b'>Bu kod {ttlMin} dakika içinde geçerlidir. Test amaçlý gönderilmiþtir.</p>
            </div>";
        await mailer.SendAsync(to!, $"{company} | OTP Test Kodu", body, isHtml: true);
    }

    var msg = mirror ? "Kod üretildi ve e-posta ile iletildi." : "Kod gönderildi.";
    return Results.Ok(new { message = msg });
}).RequireRateLimiting("otp-send");

// OTP doðrula
app.MapPost("/otp/verify", async (VerifyOtpRequest req, OtpService svc) =>
{
    var ok = await svc.VerifyAsync(req.Phone, req.Code, consumeIfValid: false);
    return ok ? Results.Ok(new { success = true })
              : Results.BadRequest(new { success = false, message = "Kod hatalý veya süresi doldu." });
});

// Form gönder (CSRF doðrulamalý)
app.MapPost("/form/submit", async (SubmitFormRequest req,
                                   OtpService svc,
                                   IEmailSender mailer,
                                   IConfiguration conf,
                                   IAntiforgery antiforgery,
                                   HttpContext ctx2) =>
{
    try { await antiforgery.ValidateRequestAsync(ctx2); }
    catch { return Results.BadRequest(new { message = "Geçersiz CSRF token." }); }

    var requireOtp = conf.GetValue("Features:RequireOtp", true);

    if (requireOtp)
    {
        if (string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.OtpCode))
            return Results.BadRequest(new { message = "Doðrulama gerekli." });

        var verified = await svc.VerifyAsync(req.Phone, req.OtpCode, consumeIfValid: true);
        if (!verified) return Results.BadRequest(new { message = "Doðrulama gerekli." });
    }
    else
    {
        if (!string.IsNullOrWhiteSpace(req.Phone) && !string.IsNullOrWhiteSpace(req.OtpCode))
            await svc.VerifyAsync(req.Phone, req.OtpCode, consumeIfValid: true);
    }

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
  <!-- Email wrapper -->
  <div style=""margin:0;padding:0;background:#f6f9fc;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif;color:#111;"">
    <div style=""max-width:720px;margin:0 auto;padding:24px 16px;"">

      <!-- Brand / divider (logo gerekmez) -->
      <div style=""padding:8px 0 6px 0;"">
        <h2 style=""margin:0 0 6px;font-size:20px;line-height:1.35;font-weight:700;"">{company}</h2>
        <div style=""height:2px;background:{primary};opacity:.6;width:100%;margin:6px 0 14px;border-radius:2px;""></div>
      </div>

      <!-- Card -->
      <div style=""background:#ffffff;border:1px solid #e6e9ef;border-radius:12px;overflow:hidden;box-shadow:0 1px 2px rgba(16,24,40,.04);"">

        <!-- Header -->
        <div style=""padding:16px 20px;border-bottom:1px solid #eef1f6;"">
          <div style=""font-size:16px;font-weight:700;margin:0 0 2px;"">Yeni Etik Hat Bildirimi</div>
          <div style=""font-size:12px;color:#64748b;"">{DateTime.Now:dd.MM.yyyy HH:mm}</div>
        </div>

        <!-- Content -->
        <div style=""padding:20px;"">

          <!-- Kategori / Tarih-Saat / Konum -->
          <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""border-collapse:collapse;"">
            <tr>
              <td style=""padding:6px 0;width:160px;color:#64748b;font-size:13px;"">Kategori</td>
              <td style=""padding:6px 0;font-size:14px;"">
                <span style=""display:inline-block;padding:4px 8px;border-radius:999px;background:{primary};color:#fff;font-weight:600;font-size:12px;"">
                  {Html(req.Category)}
                </span>
              </td>
            </tr>
            <tr>
              <td style=""padding:6px 0;width:160px;color:#64748b;font-size:13px;"">Tarih / Saat</td>
              <td style=""padding:6px 0;font-size:14px;"">{req.EventDate:dd.MM.yyyy} {req.EventTime}</td>
            </tr>
            <tr>
              <td style=""padding:6px 0;width:160px;color:#64748b;font-size:13px;"">Konum</td>
              <td style=""padding:6px 0;font-size:14px;"">{Html(req.Location)}</td>
            </tr>
            <tr>
              <td style=""padding:6px 0;width:160px;color:#64748b;font-size:13px;"">Ýlgili Kiþiler</td>
              <td style=""padding:6px 0;font-size:14px;"">{Html(req.People)}</td>
            </tr>
            {(string.IsNullOrWhiteSpace(req.Phone) ? "" : $@"
            <tr>
              <td style=""padding:6px 0;width:160px;color:#64748b;font-size:13px;"">Telefon</td>
              <td style=""padding:6px 0;font-size:14px;"">{Html(req.Phone)}</td>
            </tr>")}
            <tr>
              <td style=""padding:6px 0;width:160px;color:#64748b;font-size:13px;"">KVKK Onayý</td>
              <td style=""padding:6px 0;font-size:14px;font-weight:600;"">{(req.KvkkConsent ? "Evet" : "Hayýr")}</td>
            </tr>
          </table>

          <!-- Detay kutusu -->
          <div style=""margin-top:16px;border:1px dashed #d7dde8;background:#fafbff;border-radius:10px;padding:16px;"">
            <div style=""font-size:13px;color:#64748b;margin-bottom:6px;font-weight:600;"">Detay</div>
            <div style=""white-space:pre-wrap;font-size:14px;line-height:1.6;color:#111;"">{Html(req.Details)}</div>
          </div>

        </div>

        <!-- Footer -->
        <div style=""padding:14px 20px;border-top:1px solid #eef1f6;color:#64748b;font-size:12px;"">
          Bu e-posta sistem tarafýndan otomatik gönderilmiþtir. Yanýtlamanýza gerek yoktur.
        </div>
      </div>

      <!-- tiny spacer -->
      <div style=""height:16px""></div>

      <!-- Subtle meta -->
      <div style=""text-align:center;font-size:11px;color:#94a3b8;"">
        {company} • Etik Hat Bildirim Sistemi
      </div>

    </div>
  </div>";


    var to = conf["Mail:To"] ?? conf["Mail:From"]!;
    await mailer.SendAsync(to, $"{company} | Etik Hat Bildirimi", body, isHtml: true);

    return Results.Ok(new { message = "Bildiriminiz iletilmiþtir." });
});

// UI flag
app.MapGet("/features", (IConfiguration conf) =>
{
    var requireOtp = conf.GetValue("Features:RequireOtp", true);
    return Results.Ok(new { requireOtp });
});

app.Run();

// Request records
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
