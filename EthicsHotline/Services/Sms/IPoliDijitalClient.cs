using System.Threading;
using System.Threading.Tasks;

namespace EthicsHotline.Services.Sms
{
    public interface IPoliDijitalClient
    {
        // Gönderimler
        Task<SendSingleSmsResponse> SendSingleAsync(SendSingleSms req, CancellationToken ct = default);
        Task<SendMultiSmsResponse> SendMultiAsync(SendMultiSms req, CancellationToken ct = default);
        Task<SendDynamicSmsResponse> SendDynamicAsync(SendDynamicSms req, CancellationToken ct = default);

        // Raporlar
        Task<GetSmsReportResponse> GetReportAsync(GetSmsReport req, CancellationToken ct = default);
        Task<GetSmsReportDetailResponse> GetReportDetailAsync(GetSmsReportDetail req, CancellationToken ct = default);

        // Diğer
        Task<GetSendersResponse> GetSendersAsync(CancellationToken ct = default);
        Task<GetGatewaysResponse> GetGatewaysAsync(CancellationToken ct = default);
        Task<GetCreditResponse> GetCreditAsync(CancellationToken ct = default);
        Task<CancelResponse> CancelByIdAsync(long id, CancellationToken ct = default);
        Task<CancelResponse> CancelByCustomAsync(string customId, CancellationToken ct = default);
    }
}
