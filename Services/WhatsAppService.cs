using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TrainerBookingSystem.Web.Services;

public sealed class WhatsAppOptions
{
    public string Token { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string DefaultCountryCode { get; set; } = "44"; // GB default
}

public interface IWhatsAppService
{
    Task SendTextAsync(string rawPhone, string body, CancellationToken ct = default);
    bool IsConfigured();
}

public sealed class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opt;

    public WhatsAppService(HttpClient http, IOptions<WhatsAppOptions> opt)
    {
        _http = http;
        _opt  = opt.Value;

        if (IsConfigured())
        {
            _http.BaseAddress = new Uri($"https://graph.facebook.com/v20.0/{_opt.PhoneNumberId}/");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _opt.Token);
        }
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_opt.Token) && !string.IsNullOrWhiteSpace(_opt.PhoneNumberId);

    public async Task SendTextAsync(string rawPhone, string body, CancellationToken ct = default)
    {
        if (!IsConfigured()) return; // quietly no-op until you add the real creds

        var phone = Normalize(rawPhone, _opt.DefaultCountryCode);
        if (string.IsNullOrEmpty(phone)) return;

        var payload = new
        {
            messaging_product = "whatsapp",
            to = phone,
            type = "text",
            text = new { body }
        };

        var json = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("messages", json, ct);

        // Optional minimal logging
        if (!resp.IsSuccessStatusCode)
        {
            var msg = await resp.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"WhatsApp send failed: {(int)resp.StatusCode} {msg}");
        }
    }

    private static string Normalize(string raw, string defaultCc)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim();

        if (s.StartsWith("+")) return s;

        s = new string(s.Where(char.IsDigit).ToArray());

        if (s.StartsWith("00")) return "+" + s.Substring(2);
        if (s.StartsWith("0"))  return "+" + defaultCc + s.Substring(1);
        return "+" + s;
    }
}
