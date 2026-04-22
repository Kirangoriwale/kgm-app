namespace KgmApp.Models;

/// <summary>Payee details for UPI deep links (upi://pay). Amount is supplied per request from pending balance.</summary>
public sealed class MemberUpiPaymentOptions
{
    public const string SectionName = "MemberUpiPayment";

    /// <summary>Payee UPI ID (VPA), e.g. societyname@okicici</summary>
    public string PayeeVpa { get; set; } = string.Empty;

    /// <summary>Display name shown in UPI apps.</summary>
    public string PayeeName { get; set; } = "KGM";
}
