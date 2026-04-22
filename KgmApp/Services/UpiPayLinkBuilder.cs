using System.Globalization;

namespace KgmApp.Services;

/// <summary>Builds <c>upi://pay</c> URIs for opening the user's UPI app. Does not record or verify payments.</summary>
public static class UpiPayLinkBuilder
{
    /// <returns>null if VPA is missing or amount is not positive.</returns>
    public static string? BuildPendingDuesUri(string? payeeVpa, string? payeeName, decimal amountInr)
    {
        if (string.IsNullOrWhiteSpace(payeeVpa) || amountInr <= 0m)
            return null;

        var name = string.IsNullOrWhiteSpace(payeeName) ? "Payee" : payeeName.Trim();
        var am = amountInr.ToString("0.00", CultureInfo.InvariantCulture);
        const string note = "Pending dues — record payment in office after transfer";

        var qs =
            $"pa={Uri.EscapeDataString(payeeVpa.Trim())}" +
            $"&pn={Uri.EscapeDataString(name)}" +
            $"&am={Uri.EscapeDataString(am)}" +
            "&cu=INR" +
            $"&tn={Uri.EscapeDataString(note)}";

        return "upi://pay?" + qs;
    }
}
