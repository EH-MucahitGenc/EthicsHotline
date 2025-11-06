using System.Text.Json.Serialization;

namespace EthicsHotline.Services.Sms;

// ---- Basit tipler ----
public class Err
{
    public int Status { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public class PushSettings { /* genişletilebilir */ }

public class PeriodicSettings
{
    public int interval { get; set; }
    public int amount { get; set; }
    public int periodType { get; set; }
}

public class SmsItem
{
    public long nr { get; set; }
    public string? msg { get; set; }
    public string? xid { get; set; }
}

// ---- Liste itemları ----
public class Sender
{
    public string? Title { get; set; }
    public string? Uuid { get; set; }
    public short Status { get; set; }
}

public class Gateway
{
    public string? Uuid { get; set; }
    public short SmsType { get; set; }
    public short Operator { get; set; }
    public short SubProvider { get; set; }
    public bool IsOTP { get; set; }
    public bool SimChangeControl { get; set; }
}

// ---- Rapor modelleri ----
public class Statictics
{
    public short Total { get; set; }
    public short Delivered { get; set; }
    public short Undelivered { get; set; }
    public short Credit { get; set; }
    public short RCount { get; set; }
}

public class ReportItem
{
    public long Id { get; set; }
    public string? CustomId { get; set; }
    public short Type { get; set; }
    public string? Uuid { get; set; }
    public short State { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Sender { get; set; }
    public short Encoding { get; set; }
    public short Validity { get; set; }
    public short IsScheduled { get; set; }
    public string? SendingDate { get; set; }
    public string? ProcessingDate { get; set; }
    public Statictics Statictics { get; set; } = new();
}

public class ReportDetailItem
{
    public string? Id { get; set; }
    public string? XID { get; set; }
    public string? State { get; set; }
    public short? Credit { get; set; }
    public string? Sender { get; set; }
    public string? Target { get; set; }
    public short? Operator { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public short SetState { get; set; }
    public string? SendingDate { get; set; }
    public string? DeliveryDate { get; set; }
    public string? ProcessingDate { get; set; }
}

// ---- Request modelleri ----
public class GetSmsReportDetail
{
    public long PackageId { get; set; }
    public string? Target { get; set; }
    public int State { get; set; }
    public int Operator { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public string? CustomId { get; set; }
}

public class SendMultiSms
{
    public int Type { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = "";
    public List<long> Numbers { get; set; } = new();
    public int Encoding { get; set; }
    public string? Sender { get; set; }
    public string? Gateway { get; set; }
    public DateTime? SendingDate { get; set; }
    public int Validity { get; set; }
    public bool? Commercial { get; set; }
    public bool? SkipAhsQuery { get; set; }
    public string? CustomId { get; set; }
    public PeriodicSettings? PeriodicSettings { get; set; }
    public PushSettings? PushSettings { get; set; }
}

public class GetSmsReport
{
    public DateTime StartDate { get; set; }
    public DateTime FinishDate { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int Status { get; set; }
    public string? Senders { get; set; }
    public string? Keyword { get; set; }
    public List<long>? Ids { get; set; }
    public List<string>? CustomIds { get; set; }
}

public class SendDynamicSms
{
    public int Type { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = "";
    public List<SmsItem> Numbers { get; set; } = new();
    public int Encoding { get; set; }
    public string? Sender { get; set; }
    public string? Gateway { get; set; }
    public DateTime? SendingDate { get; set; }
    public int Validity { get; set; }
    public bool? Commercial { get; set; }
    public bool? SkipAhsQuery { get; set; }
    public string? CustomId { get; set; }
    public PeriodicSettings? PeriodicSettings { get; set; }
    public PushSettings? PushSettings { get; set; }
}

public class SendSingleSms
{
    public int Type { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = "";
    public long Number { get; set; }
    public int Encoding { get; set; }
    public string? Sender { get; set; }
    public string? Gateway { get; set; }
    public DateTime? SendingDate { get; set; }
    public int Validity { get; set; }
    public bool? Commercial { get; set; }
    public bool? SkipAhsQuery { get; set; }
    public string? CustomId { get; set; }
    public PushSettings? PushSettings { get; set; }
}

// ---- Response sarmalayıcıları ----
public class CancelResponse
{
    public short? Status { get; set; }
    public Err? Err { get; set; }
}

public class GetSendersResponse
{
    public List<Sender>? Senders { get; set; }
    public Err? Err { get; set; }
}

public class GetGatewaysResponse
{
    public List<Gateway>? Gateways { get; set; }
    public Err? Err { get; set; }
}

public class GetCreditResponse
{
    public int? Credit { get; set; }
    public Err? Err { get; set; }
}

public class SendSingleSmsResponse
{
    public int? PackageId { get; set; }
    public Err? Err { get; set; }
}

public class SendMultiSmsResponse
{
    public int? PackageId { get; set; }
    public Err? Err { get; set; }
}

public class SendDynamicSmsResponse
{
    public int? PackageId { get; set; }
    public Err? Err { get; set; }
}

public class GetSmsReportResponse
{
    public long TotalCount { get; set; }
    public List<ReportItem> List { get; set; } = new();
    public Err? Err { get; set; }
}

public class GetSmsReportDetailResponse
{
    public int TotalCount { get; set; }
    public List<ReportDetailItem> List { get; set; } = new();
    public Err? Err { get; set; }
}
