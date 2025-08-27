using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TrainerBookingSystem.Web.Services
{
    public sealed class WhatsAppOptions
    {
        public string? ApiKey { get; set; }
        public string? PhoneNumberId { get; set; }
        public string? BusinessId { get; set; }
        public bool TestMode { get; set; } = true;
    }

    public interface IWhatsAppService
    {
        bool IsConfigured { get; }
        Task SendTextAsync(string to, string message, CancellationToken ct = default);
    }

    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _http;
        private readonly ILogger<WhatsAppService> _log;
        private readonly WhatsAppOptions _opt;

        public WhatsAppService(HttpClient http, IOptions<WhatsAppOptions> opt, ILogger<WhatsAppService> log)
        {
            _http = http;
            _opt  = opt.Value;
            _log  = log;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_opt.ApiKey) &&
            !string.IsNullOrWhiteSpace(_opt.PhoneNumberId);

        public async Task SendTextAsync(string to, string message, CancellationToken ct = default)
        {
            // Safe stub when not configured
            if (!IsConfigured || _opt.TestMode)
            {
                _log.LogInformation("[WA stub] Would send to {To}: {Msg}", to, message);
                return;
            }

            // Real call (leave commented until you have creds)
            // var url = $"https://graph.facebook.com/v20.0/{_opt.PhoneNumberId}/messages";
            // using var req = new HttpRequestMessage(HttpMethod.Post, url);
            // req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
            // req.Content = JsonContent.Create(new
            // {
            //     messaging_product = "whatsapp",
            //     to,
            //     type = "text",
            //     text = new { body = message }
            // });
            // var res = await _http.SendAsync(req, ct);
            // res.EnsureSuccessStatusCode();

            await Task.CompletedTask;
        }
    }
}
