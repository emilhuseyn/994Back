using System.Text;
using ECommerce.Application.DTOs;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Common;

/// <summary>
/// Builds branded HTML bodies for the store's transactional emails.
/// Pure string-building — no I/O — so it lives in the Application layer
/// alongside the services that use it.
/// </summary>
public static class EmailTemplates
{
    private const string Brand = "CODE994";
    private const string AccentColor = "#0a0a0a";

    /// <summary>Email-verification code sent during registration / login.</summary>
    public static string VerificationCode(string fullName, string code)
    {
        var firstName = string.IsNullOrWhiteSpace(fullName) ? "" : Esc(fullName.Split(' ')[0]);
        var body = $@"
      <p style=""font-size:16px;color:#111;margin:0 0 6px;"">Salam{(firstName.Length > 0 ? $", {firstName}" : "")} 👋</p>
      <p style=""color:#555;margin:0 0 24px;"">Hesabınızı təsdiqləmək üçün aşağıdakı kodu saytda daxil edin:</p>

      <div style=""text-align:center;margin:8px 0 24px;"">
        <div style=""display:inline-block;background:#0a0a0a;color:#fff;border-radius:10px;padding:16px 28px;font-size:34px;font-weight:800;letter-spacing:10px;font-family:monospace;"">
          {Esc(code)}
        </div>
      </div>

      <p style=""color:#888;font-size:13px;margin:0;text-align:center;"">
        Kod <b>10 dəqiqə</b> ərzində etibarlıdır. Bu sorğunu siz göndərməmisinizsə, bu məktubu nəzərə almayın.
      </p>";

        return Wrap("Təsdiq kodu", body);
    }

    /// <summary>Order-confirmation email sent to the customer.</summary>
    public static string OrderConfirmation(Order order)
    {
        var rows = new StringBuilder();
        foreach (var it in order.Items)
        {
            rows.Append($@"
        <tr>
          <td style=""padding:10px 0;border-bottom:1px solid #eee;"">
            <div style=""font-weight:600;color:#111;"">{Esc(it.ProductName)}</div>
            <div style=""font-size:12px;color:#888;"">{Esc(it.ColorName)} · {Esc(it.SizeName)} × {it.Quantity}</div>
          </td>
          <td style=""padding:10px 0;border-bottom:1px solid #eee;text-align:right;white-space:nowrap;color:#111;"">
            {Money(it.TotalPrice)}
          </td>
        </tr>");
        }

        var body = $@"
      <p style=""font-size:16px;color:#111;margin:0 0 6px;"">Salam, {Esc(order.CustomerFullName)} 👋</p>
      <p style=""color:#555;margin:0 0 24px;"">Sifarişiniz uğurla qəbul edildi. Tezliklə sizinlə əlaqə saxlayacağıq.</p>

      <div style=""background:#f7f7f7;border-radius:8px;padding:16px 18px;margin-bottom:24px;"">
        <div style=""font-size:13px;color:#888;text-transform:uppercase;letter-spacing:1px;"">Sifariş nömrəsi</div>
        <div style=""font-size:20px;font-weight:700;color:#111;font-family:monospace;"">{Esc(order.OrderNumber)}</div>
      </div>

      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;margin-bottom:8px;"">
        {rows}
        <tr>
          <td style=""padding:14px 0 0;font-weight:700;color:#111;font-size:16px;"">Cəmi</td>
          <td style=""padding:14px 0 0;text-align:right;font-weight:700;color:#111;font-size:16px;"">{Money(order.TotalAmount)}</td>
        </tr>
      </table>

      <div style=""margin-top:24px;padding-top:20px;border-top:1px solid #eee;"">
        <div style=""font-size:13px;color:#888;text-transform:uppercase;letter-spacing:1px;margin-bottom:4px;"">Çatdırılma ünvanı</div>
        <div style=""color:#444;"">{Esc(order.DeliveryAddress)}</div>
      </div>";

        return Wrap("Sifariş təsdiqi", body);
    }

    /// <summary>Notification email sent to the store mailbox for a new contact message.</summary>
    public static string ContactNotification(ContactMessage msg)
    {
        var body = $@"
      <p style=""font-size:16px;color:#111;margin:0 0 18px;"">📬 Yeni əlaqə mesajı</p>

      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
        <tr><td style=""padding:8px 0;color:#888;width:90px;"">Ad</td><td style=""padding:8px 0;color:#111;font-weight:600;"">{Esc(msg.FullName)}</td></tr>
        <tr><td style=""padding:8px 0;color:#888;"">E-poçt</td><td style=""padding:8px 0;""><a href=""mailto:{Esc(msg.Email)}"" style=""color:#2563eb;"">{Esc(msg.Email)}</a></td></tr>
        {(string.IsNullOrWhiteSpace(msg.Phone) ? "" : $@"<tr><td style=""padding:8px 0;color:#888;"">Telefon</td><td style=""padding:8px 0;color:#111;"">{Esc(msg.Phone!)}</td></tr>")}
      </table>

      <div style=""margin-top:18px;padding:16px 18px;background:#f7f7f7;border-radius:8px;color:#333;line-height:1.6;white-space:pre-wrap;"">{Esc(msg.Message)}</div>";

        return Wrap("Yeni əlaqə mesajı", body);
    }

    // ─── Shared HTML shell ──────────────────────────────────────────────
    private static string Wrap(string title, string inner) => $@"<!doctype html>
<html><body style=""margin:0;padding:0;background:#fafafa;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;"">
  <div style=""max-width:560px;margin:0 auto;padding:32px 16px;"">
    <div style=""text-align:center;margin-bottom:28px;"">
      <span style=""font-size:22px;font-weight:900;letter-spacing:4px;color:{AccentColor};"">CODE<span style=""font-weight:300;"">994</span></span>
    </div>
    <div style=""background:#fff;border:1px solid #eee;border-radius:12px;padding:28px 26px;"">
      {inner}
    </div>
    <p style=""text-align:center;color:#aaa;font-size:11px;margin-top:20px;"">
      {title} · {Brand} · code994.az
    </p>
  </div>
</body></html>";

    private static string Money(decimal v) => $"{v:0.00} ₼";

    /// <summary>Minimal HTML escaping for interpolated user content.</summary>
    private static string Esc(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
