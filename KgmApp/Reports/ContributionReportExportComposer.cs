using KgmApp.Models.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace KgmApp.Reports;

public static class ContributionReportExportComposer
{
    public static byte[] GeneratePdf(ContributionReportViewModel vm) =>
        BuildDocument(vm).GeneratePdf();

    public static byte[] GenerateJpg(ContributionReportViewModel vm)
    {
        var images = BuildDocument(vm).GenerateImages();
        var firstImage = images.FirstOrDefault();
        if (firstImage is null)
            throw new InvalidOperationException("Unable to generate report image.");

        using var image = ImageSharpImage.Load(firstImage);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }

    private static IDocument BuildDocument(ContributionReportViewModel vm)
    {
        var periods = vm.Periods;
        var years = periods
            .GroupBy(p => p.FinancialYearLabel)
            .OrderBy(g => g.Key)
            .ToList();
        var rows = vm.Rows ?? [];

        var periodGrandTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in periods)
            periodGrandTotals[p.Key] = rows.Sum(r => r.PaidByPeriod.TryGetValue(p.Key, out var v) ? v : 0m);

        var registrationFeeGrandTotal = rows.Sum(r => r.RegistrationFeePaid);
        var expectedGrandTotal = rows.Sum(r => r.ExpectedTotal);
        var totalPaidGrandTotal = rows.Sum(r => r.TotalPaid);
        var pendingGrandTotal = rows.Sum(r => r.TotalPendingAmount);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x
                    .FontFamily(ReportFontDefaults.Families)
                    .FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text("Contribution Report").FontSize(14).SemiBold();
                    col.Item().Text($"Start Date: {vm.ContributionStartDate:dd-MMM-yyyy}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(0.75f).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(0.6f); // Sr
                        cols.RelativeColumn(1.7f); // Member
                        cols.RelativeColumn(1.2f); // Mobile
                        cols.RelativeColumn(1.3f); // Sub1
                        cols.RelativeColumn(1.3f); // Sub2
                        cols.RelativeColumn(1.0f); // Reg form
                        cols.RelativeColumn(1.0f); // Reg fee
                        for (var i = 0; i < periods.Count; i++)
                            cols.RelativeColumn(0.95f); // period amounts
                        cols.RelativeColumn(1.0f); // expected
                        cols.RelativeColumn(1.0f); // paid
                        cols.RelativeColumn(1.1f); // pending
                    });

                    static IContainer HeadCell(IContainer c) =>
                        c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignCenter().AlignMiddle();
                    static IContainer BodyCell(IContainer c) =>
                        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2);
                    static IContainer NumCell(IContainer c) =>
                        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().AlignMiddle();

                    // Header row 1
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Sr\nNo").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Member\nName").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Mobile\nNo").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("SubMember\n1").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("SubMember\n2").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Registration\nForm (YN)").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Registration\nFee (50)").SemiBold();

                    foreach (var y in years)
                        table.Cell().ColumnSpan((uint)y.Count()).Element(HeadCell).Text(y.Key).SemiBold();

                    table.Cell().RowSpan(2).Element(HeadCell).Text("Expected\nTotal").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Total\nPaid").SemiBold();
                    table.Cell().RowSpan(2).Element(HeadCell).Text("Total Pending\nAmount").SemiBold();

                    // Header row 2 (period labels)
                    foreach (var y in years)
                    {
                        foreach (var p in y)
                            table.Cell().Element(HeadCell).Text($"{p.PeriodLabel}\n(200)").FontSize(7.5f).SemiBold();
                    }

                    // Body
                    foreach (var r in rows)
                    {
                        table.Cell().Element(BodyCell).Text(r.SrNo.ToString());
                        table.Cell().Element(BodyCell).Text(r.MemberName);
                        table.Cell().Element(BodyCell).Text(r.MobileNo);
                        table.Cell().Element(BodyCell).Text(r.SubMember1);
                        table.Cell().Element(BodyCell).Text(r.SubMember2);
                        table.Cell().Element(BodyCell).AlignCenter().Text(r.RegistrationFormSubmitted ? "Y" : "N");
                        table.Cell().Element(NumCell).Text(r.RegistrationFeePaid <= 0 ? "" : r.RegistrationFeePaid.ToString("0"));

                        foreach (var p in periods)
                        {
                            r.PaidByPeriod.TryGetValue(p.Key, out var v);
                            table.Cell().Element(NumCell).Text(v <= 0 ? "" : v.ToString("0"));
                        }

                        table.Cell().Element(NumCell).Text(r.ExpectedTotal <= 0 ? "" : r.ExpectedTotal.ToString("0"));
                        table.Cell().Element(NumCell).Text(r.TotalPaid <= 0 ? "" : r.TotalPaid.ToString("0"));
                        table.Cell().Element(NumCell).Text(r.TotalPendingAmount <= 0 ? "" : r.TotalPendingAmount.ToString("0"));
                    }

                    // Footer / Grand totals
                    IContainer FooterCell(IContainer c) =>
                        c.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3);

                    table.Cell().Element(FooterCell).Text("Grand\nTotal").SemiBold();
                    table.Cell().ColumnSpan(5).Element(FooterCell).Text("");
                    table.Cell().Element(FooterCell).AlignRight().Text(registrationFeeGrandTotal <= 0 ? "" : registrationFeeGrandTotal.ToString("0")).SemiBold();

                    foreach (var p in periods)
                    {
                        periodGrandTotals.TryGetValue(p.Key, out var t);
                        table.Cell().Element(FooterCell).AlignRight().Text(t <= 0 ? "" : t.ToString("0")).SemiBold();
                    }

                    table.Cell().Element(FooterCell).AlignRight().Text(expectedGrandTotal <= 0 ? "" : expectedGrandTotal.ToString("0")).SemiBold();
                    table.Cell().Element(FooterCell).AlignRight().Text(totalPaidGrandTotal <= 0 ? "" : totalPaidGrandTotal.ToString("0")).SemiBold();
                    table.Cell().Element(FooterCell).AlignRight().Text(pendingGrandTotal <= 0 ? "" : pendingGrandTotal.ToString("0")).SemiBold();
                });

                page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Medium))
                    .Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
            });
        });
    }
}
