using KgmApp.Models.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KgmApp.Reports;

public static class StatementPdfComposer
{
    private static class Theme
    {
        public static readonly Color Navy = Color.FromHex("#1e3a5f");
        public static readonly Color NavyLight = Color.FromHex("#e8eef5");
        public static readonly Color PageBg = Color.FromHex("#f8fafc");
        public static readonly Color Border = Color.FromHex("#cbd5e1");
        /// <summary>Very light row / rule color (replaces heavy grid lines).</summary>
        public static readonly Color RuleLight = Color.FromHex("#eef2f7");
        public static readonly Color Muted = Color.FromHex("#64748b");

        public static readonly Color Cash = Color.FromHex("#d97706");
        public static readonly Color CashBg = Color.FromHex("#fffbeb");
        public static readonly Color Bank = Color.FromHex("#2563eb");
        public static readonly Color BankBg = Color.FromHex("#eff6ff");
        public static readonly Color Total = Color.FromHex("#059669");
        public static readonly Color TotalBg = Color.FromHex("#ecfdf5");

        public static readonly Color IncomeHeader = Color.FromHex("#047857");
        public static readonly Color IncomeRowAlt = Color.FromHex("#f0fdf4");
        public static readonly Color ExpenseHeader = Color.FromHex("#b91c1c");
        public static readonly Color ExpenseRowAlt = Color.FromHex("#fef2f2");

        public static readonly Color TransferHeader = Color.FromHex("#5b21b6");
        public static readonly Color TransferBg = Color.FromHex("#f5f3ff");
    }

    public static byte[] Generate(StatementReportViewModel vm)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor(Theme.PageBg);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Color.FromHex("#334155")));

                page.Header().Element(c => RenderPageHeader(c, vm));

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(11);

                    column.Item().Element(c => SectionCard(c, "Opening balances", Theme.Navy, inner =>
                    {
                        inner.Spacing(0);
                        inner.Item().Element(x => BalanceLine(x, "Cash in hand", vm.OpeningCashInHand, Theme.Cash, Theme.CashBg));
                        inner.Item().Element(x => BalanceLine(x, "Cash in bank", vm.OpeningBankBalance, Theme.Bank, Theme.BankBg));
                        inner.Item().Element(x => BalanceLine(x, "Total cash (hand + bank)", vm.OpeningTotalCash, Theme.Total, Theme.TotalBg, boldAmount: true));
                    }));

                    column.Item().Element(c => SectionCard(c, "Income (head-wise)", Theme.IncomeHeader, inner =>
                    {
                        inner.Item().Element(x => LinesTable(x, vm.IncomeLines, vm.TotalIncomeInPeriod, "Total income", Theme.IncomeHeader, Theme.IncomeRowAlt));
                    }));

                    column.Item().Element(c => SectionCard(c, "Expenses (head-wise)", Theme.ExpenseHeader, inner =>
                    {
                        inner.Item().Element(x => LinesTable(x, vm.ExpenseLines, vm.TotalExpenseInPeriod, "Total expenses", Theme.ExpenseHeader, Theme.ExpenseRowAlt));
                    }));

                    column.Item().Element(c => SectionCard(c, "Transfers (cash ↔ bank)", Theme.TransferHeader, inner =>
                    {
                        inner.Item().Element(x => TransferBlock(x, vm));
                    }));

                    column.Item().Element(c => SectionCard(c, "Closing balances", Theme.Navy, inner =>
                    {
                        inner.Spacing(0);
                        inner.Item().Element(x => BalanceLine(x, "Cash in hand", vm.ClosingCashInHand, Theme.Cash, Theme.CashBg));
                        inner.Item().Element(x => BalanceLine(x, "Cash in bank", vm.ClosingBankBalance, Theme.Bank, Theme.BankBg));
                        inner.Item().Element(x => BalanceLine(x, "Total cash (hand + bank)", vm.ClosingTotalCash, Theme.Total, Theme.TotalBg, boldAmount: true));
                    }));
                });

                page.Footer().Element(RenderFooter);
            });
        }).GeneratePdf();
    }

    private static void RenderPageHeader(IContainer container, StatementReportViewModel vm)
    {
        container.Column(col =>
        {
            col.Item().Background(Theme.Navy).PaddingHorizontal(14).PaddingVertical(10).Column(inner =>
            {
                inner.Item().Text("Statement").FontSize(16).Bold().FontColor(Colors.White);
                inner.Item().PaddingTop(4).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(10).FontColor(Color.FromHex("#cbd5e1")));
                    t.Span("Khamgaon Gramstha Mandal");
                });
                inner.Item().PaddingTop(4).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(10.5f));
                    t.Span("Period: ").FontColor(Color.FromHex("#94a3b8"));
                    t.Span($"{vm.FromDate:dd MMM yyyy}").SemiBold().FontColor(Colors.White);
                    t.Span("  →  ").FontColor(Color.FromHex("#64748b"));
                    t.Span($"{vm.ToDate:dd MMM yyyy}").SemiBold().FontColor(Colors.White);
                    t.Span("  (inclusive)").FontColor(Color.FromHex("#94a3b8"));
                });
            });

            col.Item().Height(3).Background(Color.FromHex("#0ea5e9"));
        });
    }

    private static void SectionCard(IContainer container, string title, Color titleBarColor, Action<ColumnDescriptor> content)
    {
        container.Background(Colors.White)
            .Border(1)
            .BorderColor(Theme.Border)
            .Column(col =>
            {
                col.Item().Background(titleBarColor).PaddingHorizontal(10).PaddingVertical(5)
                    .Text(title).FontSize(10).Bold().FontColor(Colors.White);
                col.Item().Background(Theme.NavyLight).Padding(6).Column(content);
            });
    }

    private static void BalanceLine(IContainer container, string label, decimal amount, Color accent, Color rowBg, bool boldAmount = false)
    {
        container.Background(rowBg)
            .BorderLeft(3)
            .BorderColor(accent)
            .PaddingVertical(5)
            .PaddingHorizontal(8)
            .Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(label).FontSize(9.5f).SemiBold().FontColor(Color.FromHex("#1e293b"));
                var amt = row.ConstantItem(110).AlignRight().AlignMiddle();
                if (boldAmount)
                    amt.Text(amount.ToString("N2")).FontSize(11).Bold().FontColor(accent);
                else
                    amt.Text(amount.ToString("N2")).FontSize(10).SemiBold().FontColor(Color.FromHex("#0f172a"));
            });
    }

    private static void TransferBlock(IContainer container, StatementReportViewModel vm)
    {
        container.Background(Theme.TransferBg)
            .Border(1)
            .BorderColor(Color.FromHex("#ddd6fe"))
            .Padding(6)
            .Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Cash deposit to bank").FontSize(9).FontColor(Theme.Muted);
                    r.ConstantItem(100).AlignRight().Text(vm.PeriodCashDepositToBank.ToString("N2")).SemiBold().FontColor(Color.FromHex("#1e293b"));
                });
                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Bank withdrawal").FontSize(9).FontColor(Theme.Muted);
                    r.ConstantItem(100).AlignRight().Text(vm.PeriodBankWithdrawal.ToString("N2")).SemiBold().FontColor(Color.FromHex("#1e293b"));
                });
            });
    }

    private static void LinesTable(
        IContainer container,
        IReadOnlyList<StatementLine> lines,
        decimal total,
        string totalLabel,
        Color headerColor,
        Color zebraTint)
    {
        if (lines.Count == 0)
        {
            container.Padding(6).Text("No entries in this period.").Italic().FontSize(8.5f).FontColor(Theme.Muted);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2f);
                cols.RelativeColumn(3f);
                cols.RelativeColumn(1.2f);
            });

            table.Header(header =>
            {
                // No underline under column titles — solid header bar only.
                IContainer HeaderCell(IContainer c, Color bg) =>
                    c.Background(bg).PaddingVertical(4).PaddingHorizontal(5);

                header.Cell().Element(c => HeaderCell(c, headerColor)).Text("Head (category)").FontColor(Colors.White).SemiBold().FontSize(8.5f);
                header.Cell().Element(c => HeaderCell(c, headerColor)).Text("Description").FontColor(Colors.White).SemiBold().FontSize(8.5f);
                header.Cell().Element(c => HeaderCell(c, headerColor)).AlignRight().Text("Amount").FontColor(Colors.White).SemiBold().FontSize(8.5f);
            });

            var i = 0;
            foreach (var line in lines)
            {
                var desc = line.ShowDescription
                    ? (string.IsNullOrEmpty(line.Description) ? "—" : line.Description)
                    : "—";

                var rowBg = i % 2 == 0 ? Colors.White : zebraTint;
                i++;

                // Light zebra only — no heavy grid lines between rows.
                IContainer BodyCell(IContainer c, Color bg) =>
                    c.Background(bg).PaddingVertical(3).PaddingHorizontal(5);

                table.Cell().Element(c => BodyCell(c, rowBg)).Text(line.Head).FontSize(8.5f);
                table.Cell().Element(c => BodyCell(c, rowBg)).Text(desc).FontSize(8f).FontColor(line.ShowDescription && !string.IsNullOrEmpty(line.Description) ? Color.FromHex("#475569") : Theme.Muted);
                table.Cell().Element(c => BodyCell(c, rowBg)).AlignRight().Text(line.Amount.ToString("N2")).SemiBold().FontSize(8.5f);
            }

            table.Footer(footer =>
            {
                // Thin, light separator above totals (not a thick accent bar).
                IContainer TotalCell(IContainer c) =>
                    c.Background(Color.FromHex("#f8fafc")).PaddingVertical(4).PaddingHorizontal(5).BorderTop(0.5f).BorderColor(Theme.RuleLight);

                footer.Cell().ColumnSpan(2).Element(TotalCell).Text(totalLabel).Bold().FontSize(9).FontColor(Color.FromHex("#0f172a"));
                footer.Cell().Element(TotalCell).AlignRight().Text(total.ToString("N2")).Bold().FontSize(9).FontColor(headerColor);
            });
        });
    }

    private static void RenderFooter(IContainer container)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().LineHorizontal(0.35f).LineColor(Theme.RuleLight);
            col.Item().PaddingTop(5).AlignCenter().DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Theme.Muted))
                .Text(t =>
                {
                    t.Span("Khamgaon Gramstha Mandal · Statement · Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
        });
    }
}
