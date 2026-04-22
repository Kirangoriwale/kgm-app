using KgmApp.Models.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KgmApp.Reports;

public static class AttendanceReportPdfComposer
{
    public static byte[] Generate(AttendanceReportViewModel vm, string reportTitle = "Attendance Report")
    {
        var meetings = vm.Meetings ?? [];
        var rows = vm.Rows ?? [];
        var absentCount = rows.Count(r => string.Equals(r.Remark, "Absent", StringComparison.OrdinalIgnoreCase));

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Color.FromHex("#1f2937")));

                page.Header().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().Text(reportTitle).FontSize(14).Bold().FontColor(Color.FromHex("#111827"));
                    col.Item().Text($"Generated on {DateTime.Today:dd-MMM-yyyy}")
                        .FontSize(8)
                        .FontColor(Color.FromHex("#6b7280"));
                    col.Item().Text($"Filtered Members: {rows.Count} | Meetings: {meetings.Count}")
                        .FontSize(8)
                        .FontColor(Color.FromHex("#4b5563"));
                    col.Item().PaddingTop(4).LineHorizontal(0.7f).LineColor(Color.FromHex("#d1d5db"));
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(0.7f);  // Sr
                        cols.RelativeColumn(2.2f);  // Member Name
                        cols.RelativeColumn(1.4f);  // Mobile
                        for (var i = 0; i < meetings.Count; i++)
                            cols.RelativeColumn(0.9f); // P/A by meeting
                        cols.RelativeColumn(0.9f);  // Score
                        cols.RelativeColumn(1.0f);  // Remark
                    });

                    static IContainer HeadCell(IContainer c) =>
                        c.Background(Color.FromHex("#111827"))
                            .Border(0.5f)
                            .BorderColor(Color.FromHex("#374151"))
                            .PaddingVertical(4)
                            .PaddingHorizontal(3)
                            .AlignCenter()
                            .AlignMiddle();

                    static IContainer SubHeadCell(IContainer c) =>
                        c.Background(Color.FromHex("#dbeafe"))
                            .Border(0.5f)
                            .BorderColor(Color.FromHex("#bfdbfe"))
                            .PaddingVertical(3)
                            .PaddingHorizontal(2)
                            .AlignCenter()
                            .AlignMiddle();

                    static IContainer BodyCell(IContainer c) =>
                        c.Border(0.5f)
                            .BorderColor(Color.FromHex("#e5e7eb"))
                            .PaddingVertical(2.5f)
                            .PaddingHorizontal(3)
                            .AlignMiddle();

                    static IContainer NumCell(IContainer c) =>
                        c.Border(0.5f)
                            .BorderColor(Color.FromHex("#e5e7eb"))
                            .PaddingVertical(2.5f)
                            .PaddingHorizontal(3)
                            .AlignCenter()
                            .AlignMiddle();

                    // Header row
                    table.Cell().Element(HeadCell).Text("Sr").FontColor(Colors.White).SemiBold();
                    table.Cell().Element(HeadCell).Text("Member Name").FontColor(Colors.White).SemiBold();
                    table.Cell().Element(HeadCell).Text("Mobile No").FontColor(Colors.White).SemiBold();

                    foreach (var mt in meetings)
                    {
                        table.Cell().Element(SubHeadCell).Column(col =>
                        {
                            col.Item().AlignCenter().Text(mt.MeetingDate.ToString("dd-MMM-yy")).SemiBold().FontSize(7.5f);
                            col.Item().AlignCenter().Text($"({mt.PresentPercent:0.##}%)").FontSize(7).FontColor(Color.FromHex("#475569"));
                        });
                    }

                    table.Cell().Element(c => c.Background(Color.FromHex("#cffafe"))
                        .Border(0.5f)
                        .BorderColor(Color.FromHex("#a5f3fc"))
                        .Padding(3)
                        .AlignCenter()
                        .AlignMiddle())
                        .Text("Score").SemiBold();

                    table.Cell().Element(c => c.Background(Color.FromHex("#fef3c7"))
                        .Border(0.5f)
                        .BorderColor(Color.FromHex("#fde68a"))
                        .Padding(3)
                        .AlignCenter()
                        .AlignMiddle())
                        .Text("Remark").SemiBold();

                    // Body rows
                    foreach (var row in rows)
                    {
                        var presentCount = row.PresenceByMeeting.Values.Count(v => v);
                        var totalMeetings = meetings.Count;

                        table.Cell().Element(NumCell).Text(row.Sr.ToString());
                        table.Cell().Element(BodyCell).Text(row.MemberName);
                        table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(row.MobileNo) ? "-" : row.MobileNo);

                        foreach (var mt in meetings)
                        {
                            var present = row.PresenceByMeeting.TryGetValue(mt.MeetingId, out var isPresent) && isPresent;
                            table.Cell().Element(NumCell).Text(present ? "P" : "A")
                                .FontColor(present ? Color.FromHex("#166534") : Color.FromHex("#991b1b"))
                                .SemiBold();
                        }

                        table.Cell().Element(NumCell).Text($"{presentCount}/{totalMeetings}").SemiBold();
                        table.Cell().Element(NumCell).Text(row.Remark);
                    }

                    // Grand total
                    IContainer FooterCell(IContainer c) =>
                        c.Background(Color.FromHex("#111827"))
                            .Border(0.5f)
                            .BorderColor(Color.FromHex("#374151"))
                            .PaddingVertical(4)
                            .PaddingHorizontal(3)
                            .AlignCenter()
                            .AlignMiddle();

                    table.Cell().Element(FooterCell).Text("Grand Total").FontColor(Colors.White).SemiBold();
                    table.Cell().ColumnSpan(2).Element(FooterCell).Text("").FontColor(Colors.White);

                    foreach (var mt in meetings)
                        table.Cell().Element(FooterCell).Text(mt.PresentCount.ToString()).FontColor(Colors.White).SemiBold();

                    table.Cell().Element(FooterCell).Text("").FontColor(Colors.White);
                    table.Cell().Element(FooterCell).Text(absentCount.ToString()).FontColor(Colors.White).SemiBold();
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Color.FromHex("#6b7280")));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
