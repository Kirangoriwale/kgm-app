namespace KgmApp.Reports;

internal static class ReportFontDefaults
{
    // Devanagari-capable fonts first, then broad Unicode fallbacks.
    internal static readonly string[] Families =
    [
        "Nirmala UI",
        "Mangal",
        "Noto Sans Devanagari",
        "Arial Unicode MS",
        "Segoe UI",
        "Arial"
    ];
}
