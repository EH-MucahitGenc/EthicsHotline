using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace EthicsHotline.Services.Sms
{
    public class PoliDijitalClient : IPoliDijitalClient
    {
        private readonly HttpClient _http;
        private readonly PoliDijitalOptions _opt;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public PoliDijitalClient(HttpClient http, IOptions<PoliDijitalOptions> opt)
        {
            _http = http;
            _opt = opt.Value;

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.Username}:{_opt.Password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        // ----------------- Helpers -----------------
        private static StringContent J(object o) =>
            new(JsonSerializer.Serialize(o, JsonOpts), Encoding.UTF8, "application/json");

        private static T Parse<T>(string json) where T : new()
        {
            using var doc = JsonDocument.Parse(json);
            // Başarılı yanıt (data)
            if (doc.RootElement.TryGetProperty("data", out var _))
            {
                // Bazı listeler ve bilgilendirme uçları doğrudan { data: {...} } döner;
                // DTO'ların "Err" alanı yoksa da sorun olmaz, direkt deserialize edebiliriz.
                return JsonSerializer.Deserialize<T>(json)!;
            }

            // Hatalı yanıt (err)
            var root = doc.RootElement;
            if (root.TryGetProperty("err", out var err))
            {
                var obj = new T();
                var pErr = typeof(T).GetProperty("Err");
                if (pErr != null && pErr.PropertyType == typeof(Err))
                {
                    var e = new Err
                    {
                        Status = err.TryGetProperty("status", out var s) ? s.GetInt32() : 0,
                        Code = err.TryGetProperty("code", out var c) ? c.GetString() : null,
                        Message = err.TryGetProperty("message", out var m) ? m.GetString() : null
                    };
                    pErr.SetValue(obj, e);
                }
                return obj;
            }

            // Beklenmeyen format
            throw new InvalidOperationException("PoliDijital: Beklenmeyen yanıt formatı.");
        }

        private static TR ParseCreate<TR>(string json) where TR : new()
        {
            using var doc = JsonDocument.Parse(json);

            // Başarılı ise data.pkgID var
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
            {
                var ok = new TR();
                typeof(TR).GetProperty("PackageId")?.SetValue(ok, data.GetProperty("pkgID").GetInt32());
                typeof(TR).GetProperty("Err")?.SetValue(ok, null);
                return ok;
            }

            // Hata
            var errEl = doc.RootElement.GetProperty("err");
            var fail = new TR();
            typeof(TR).GetProperty("Err")?.SetValue(fail, new Err
            {
                Status = errEl.TryGetProperty("status", out var s) ? s.GetInt32() : 0,
                Code = errEl.TryGetProperty("code", out var c) ? c.GetString() : null,
                Message = errEl.TryGetProperty("message", out var m) ? m.GetString() : null
            });
            return fail;
        }

        private static int NormalizePageSize(int ps) => ps <= 0 ? 1000 : Math.Min(ps, 1000);

        // ----------------- Gönderimler -----------------
        public async Task<SendSingleSmsResponse> SendSingleAsync(SendSingleSms r, CancellationToken ct = default)
        {
            // API payload şekli senin eski client’taki ile uyumlu
            var payload = new
            {
                type = r.Type == 0 ? 1 : r.Type,
                sendingType = 0,
                title = string.IsNullOrWhiteSpace(r.Title) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : r.Title,
                content = r.Content,
                number = r.Number.ToString(),
                encoding = r.Encoding,
                sender = r.Sender ?? _opt.Sender,
                //validity = r.Validity == 0 ? _opt.Validity : r.Validity,
                commercial = r.Commercial ?? _opt.Commercial,
                skipAhsQuery = r.SkipAhsQuery ?? _opt.SkipAhsQuery,
                customID = string.IsNullOrWhiteSpace(r.CustomId) ? null : r.CustomId,
                gateway = string.IsNullOrWhiteSpace(r.Gateway) ? _opt.Gateway : r.Gateway,
                sendingDate = r.SendingDate.HasValue ? r.SendingDate.Value.ToString("yyyy-MM-dd HH:mm") : null,
                pushSettings = r.PushSettings
            };

            using var res = await _http.PostAsync("sms/create-otp", J(payload), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseCreate<SendSingleSmsResponse>(json);
        }

        public async Task<SendMultiSmsResponse> SendMultiAsync(SendMultiSms r, CancellationToken ct = default)
        {
            var payload = new
            {
                type = r.Type == 0 ? 1 : r.Type,
                sendingType = 1,
                title = string.IsNullOrWhiteSpace(r.Title) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : r.Title,
                content = r.Content,
                numbers = r.Numbers,
                encoding = r.Encoding,
                sender = r.Sender ?? _opt.Sender,
                validity = r.Validity == 0 ? _opt.Validity : r.Validity,
                commercial = r.Commercial ?? _opt.Commercial,
                skipAhsQuery = r.SkipAhsQuery ?? _opt.SkipAhsQuery,
                customID = string.IsNullOrWhiteSpace(r.CustomId) ? null : r.CustomId,
                gateway = string.IsNullOrWhiteSpace(r.Gateway) ? _opt.Gateway : r.Gateway,
                sendingDate = r.SendingDate.HasValue ? r.SendingDate.Value.ToString("yyyy-MM-dd HH:mm") : null,
                periodicSettings = r.PeriodicSettings,
                pushSettings = r.PushSettings
            };

            using var res = await _http.PostAsync("sms/create", J(payload), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseCreate<SendMultiSmsResponse>(json);
        }

        public async Task<SendDynamicSmsResponse> SendDynamicAsync(SendDynamicSms r, CancellationToken ct = default)
        {
            var payload = new
            {
                type = r.Type == 0 ? 1 : r.Type,
                sendingType = 2,
                title = string.IsNullOrWhiteSpace(r.Title) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : r.Title,
                content = r.Content,
                numbers = r.Numbers, // [{nr,msg,xid}]
                encoding = r.Encoding,
                sender = r.Sender ?? _opt.Sender,
                validity = r.Validity == 0 ? _opt.Validity : r.Validity,
                commercial = r.Commercial ?? _opt.Commercial,
                skipAhsQuery = r.SkipAhsQuery ?? _opt.SkipAhsQuery,
                customID = string.IsNullOrWhiteSpace(r.CustomId) ? null : r.CustomId,
                gateway = string.IsNullOrWhiteSpace(r.Gateway) ? _opt.Gateway : r.Gateway,
                sendingDate = r.SendingDate.HasValue ? r.SendingDate.Value.ToString("yyyy-MM-dd HH:mm") : null,
                periodicSettings = r.PeriodicSettings,
                pushSettings = r.PushSettings
            };

            using var res = await _http.PostAsync("sms/create", J(payload), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseCreate<SendDynamicSmsResponse>(json);
        }

        // ----------------- Raporlar -----------------
        public async Task<GetSmsReportResponse> GetReportAsync(GetSmsReport r, CancellationToken ct = default)
        {
            object payload;

            if (r.Ids is { Count: > 0 })
            {
                payload = new
                {
                    ids = r.Ids,
                    pageIndex = r.PageIndex,
                    pageSize = NormalizePageSize(r.PageSize),
                    status = r.Status,
                    senders = r.Senders,
                    keyword = r.Keyword
                };
            }
            else if (r.CustomIds is { Count: > 0 })
            {
                payload = new
                {
                    customIDs = r.CustomIds,
                    pageIndex = r.PageIndex,
                    pageSize = NormalizePageSize(r.PageSize),
                    status = r.Status,
                    senders = r.Senders,
                    keyword = r.Keyword
                };
            }
            else
            {
                if (r.StartDate == default || r.FinishDate == default || r.FinishDate <= r.StartDate)
                    return new GetSmsReportResponse { Err = new Err { Status = 400, Code = "Bad Request!", Message = "Başlangıç/Bitiş tarihini kontrol edin." } };

                payload = new
                {
                    startDate = r.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    finishDate = r.FinishDate.ToString("yyyy-MM-dd HH:mm"),
                    pageIndex = r.PageIndex,
                    pageSize = NormalizePageSize(r.PageSize),
                    status = r.Status,
                    senders = r.Senders,
                    keyword = r.Keyword
                };
            }

            using var res = await _http.PostAsync("sms/list", J(payload), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<GetSmsReportResponse>(json);
        }

        public async Task<GetSmsReportDetailResponse> GetReportDetailAsync(GetSmsReportDetail r, CancellationToken ct = default)
        {
            if ((r.PackageId == 0 && string.IsNullOrWhiteSpace(r.CustomId)) ||
                (r.PackageId != 0 && !string.IsNullOrWhiteSpace(r.CustomId)))
            {
                return new GetSmsReportDetailResponse
                {
                    Err = new Err { Status = 400, Code = "Bad Request!", Message = "PackageId ya da CustomId (yalnızca biri) gönderiniz." }
                };
            }

            var payload = new
            {
                pkgID = r.PackageId == 0 ? (long?)null : r.PackageId,
                customID = r.PackageId == 0 ? r.CustomId : null,
                target = string.IsNullOrWhiteSpace(r.Target) ? null : r.Target,
                state = r.State == 0 ? (int?)null : r.State,
                Operator = r.Operator == 0 ? (int?)null : r.Operator,
                pageIndex = r.PageIndex,
                pageSize = NormalizePageSize(r.PageSize)
            };

            using var res = await _http.PostAsync("sms/list-item", J(payload), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<GetSmsReportDetailResponse>(json);
        }

        // ----------------- Diğer -----------------
        public async Task<GetSendersResponse> GetSendersAsync(CancellationToken ct = default)
        {
            using var res = await _http.PostAsync("sms/list-sender", J(""), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<GetSendersResponse>(json);
        }

        public async Task<GetGatewaysResponse> GetGatewaysAsync(CancellationToken ct = default)
        {
            using var res = await _http.GetAsync("sms/list-gateway", ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<GetGatewaysResponse>(json);
        }

        public async Task<GetCreditResponse> GetCreditAsync(CancellationToken ct = default)
        {
            using var res = await _http.PostAsync("user/credit", J(""), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<GetCreditResponse>(json);
        }

        public async Task<CancelResponse> CancelByIdAsync(long id, CancellationToken ct = default)
        {
            using var res = await _http.PostAsync("sms/cancel", J(new { id }), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<CancelResponse>(json);
        }

        public async Task<CancelResponse> CancelByCustomAsync(string customId, CancellationToken ct = default)
        {
            using var res = await _http.PostAsync("sms/cancel", J(new { customID = customId }), ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            return Parse<CancelResponse>(json);
        }
    }
}
