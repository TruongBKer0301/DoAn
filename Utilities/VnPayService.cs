using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace LapTopBD.Utilities
{
    public class VnPayOptions
    {
        public string TmnCode { get; set; } = string.Empty;
        public string HashSecret { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        public string Command { get; set; } = "pay";
        public string CurrCode { get; set; } = "VND";
        public string Version { get; set; } = "2.1.0";
        public string Locale { get; set; } = "vn";
        public string PaymentBackReturnUrl { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    }

    public class VnPayReturnResult
    {
        public bool IsValidSignature { get; set; }
        public bool IsSuccess { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
    }

    public interface IVnPayService
    {
        string CreatePaymentUrl(HttpContext httpContext, long amount, string orderInfo, string transactionRef);
        VnPayReturnResult ProcessReturn(IQueryCollection query);
    }

    public class VnPayService : IVnPayService
    {
        private readonly VnPayOptions _options;
        private readonly ILogger<VnPayService> _logger;

        public VnPayService(IOptions<VnPayOptions> options, ILogger<VnPayService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public string CreatePaymentUrl(HttpContext httpContext, long amount, string orderInfo, string transactionRef)
        {
            var now = DateTime.UtcNow;
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
                now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                now = DateTime.UtcNow.AddHours(7);
            }

            var vnpData = new SortedDictionary<string, string>
            {
                ["vnp_Version"] = _options.Version,
                ["vnp_Command"] = _options.Command,
                ["vnp_TmnCode"] = _options.TmnCode,
                ["vnp_Amount"] = (amount * 100).ToString(CultureInfo.InvariantCulture),
                ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
                ["vnp_CurrCode"] = _options.CurrCode,
                ["vnp_IpAddr"] = GetIpAddress(httpContext),
                ["vnp_Locale"] = _options.Locale,
                ["vnp_OrderInfo"] = orderInfo,
                ["vnp_OrderType"] = "other",
                ["vnp_ReturnUrl"] = _options.PaymentBackReturnUrl,
                ["vnp_TxnRef"] = transactionRef,
                ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss")
            };

            var hashData = BuildHashData(vnpData);
            var queryString = BuildRequestQuery(vnpData);
            var secureHash = ComputeHmacSha512(_options.HashSecret.Trim(), hashData);

            _logger.LogInformation(
                "VNPay CreatePaymentUrl TmnCode={TmnCode} TxnRef={TxnRef} Amount={Amount} HashData={HashData} SecureHash={SecureHash}",
                _options.TmnCode,
                transactionRef,
                amount,
                hashData,
                secureHash);

            return $"{_options.BaseUrl}?{queryString}&vnp_SecureHashType=HMACSHA512&vnp_SecureHash={secureHash}";
        }

        public VnPayReturnResult ProcessReturn(IQueryCollection query)
        {
            var result = new VnPayReturnResult();

            if (!query.TryGetValue("vnp_SecureHash", out var receivedHash))
            {
                _logger.LogWarning("VNPay return missing secure hash.");
                return result;
            }

            var vnpData = new SortedDictionary<string, string>();
            foreach (var kv in query)
            {
                if (kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(kv.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(kv.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kv.Value))
                {
                    vnpData[kv.Key] = kv.Value.ToString();
                }
            }

            var signData = BuildHashData(vnpData);
            var calculatedHash = ComputeHmacSha512(_options.HashSecret.Trim(), signData);
            var validSignature = string.Equals(calculatedHash, receivedHash.ToString(), StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "VNPay ProcessReturn TxnRef={TxnRef} ResponseCode={ResponseCode} TransactionStatus={TransactionStatus} SignData={SignData} ReceivedHash={ReceivedHash} CalculatedHash={CalculatedHash} ValidSignature={ValidSignature}",
                vnpData.GetValueOrDefault("vnp_TxnRef", string.Empty),
                vnpData.GetValueOrDefault("vnp_ResponseCode", string.Empty),
                vnpData.GetValueOrDefault("vnp_TransactionStatus", string.Empty),
                signData,
                receivedHash.ToString(),
                calculatedHash,
                validSignature);

            result.IsValidSignature = validSignature;
            result.TransactionRef = vnpData.GetValueOrDefault("vnp_TxnRef", string.Empty);
            result.ResponseCode = vnpData.GetValueOrDefault("vnp_ResponseCode", string.Empty);
            result.TransactionStatus = vnpData.GetValueOrDefault("vnp_TransactionStatus", string.Empty);
            result.OrderInfo = vnpData.GetValueOrDefault("vnp_OrderInfo", string.Empty);

            if (long.TryParse(vnpData.GetValueOrDefault("vnp_Amount", "0"), out var amount))
            {
                result.Amount = amount / 100;
            }

            result.IsSuccess = validSignature
                && string.Equals(result.ResponseCode, "00", StringComparison.OrdinalIgnoreCase)
                && string.Equals(result.TransactionStatus, "00", StringComparison.OrdinalIgnoreCase);

            return result;
        }

        private static string BuildHashData(SortedDictionary<string, string> data)
        {
            return string.Join("&", data
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{x.Key}={WebUtility.UrlEncode(x.Value)}"));
        }

        private static string BuildRequestQuery(SortedDictionary<string, string> data)
        {
            return string.Join("&", data
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{UrlEncoder.Default.Encode(x.Key)}={UrlEncoder.Default.Encode(x.Value)}"));
        }

        private static string ComputeHmacSha512(string key, string inputData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);
            using var hmac = new HMACSHA512(keyBytes);
            var hashValue = hmac.ComputeHash(inputBytes);
            return BitConverter.ToString(hashValue).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string GetIpAddress(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ip) || ip == "::1")
            {
                return "127.0.0.1";
            }

            return ip;
        }
    }
}
