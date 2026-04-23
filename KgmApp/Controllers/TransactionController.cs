using KgmApp.Data;
using KgmApp.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class TransactionController : Controller
{
    private readonly AppDbContext _db;
    private static readonly string[] AllowedCategories =
    [
        Transaction.CategoryMemberRegistration,
        Transaction.CategorySubMemberRegistration,
        Transaction.CategoryHalfYearlyContribution,
        Transaction.CategoryDonation,
        Transaction.CategoryBankInterest,
        Transaction.CategoryBankCharges,
        Transaction.CategoryCashDepositToBank,
        Transaction.CategoryBankWithdrawal,
        Transaction.CategoryExpense
    ];
    private static readonly string[] AllowedPaymentModes =
    [
        Transaction.PaymentModeCash,
        Transaction.PaymentModeOnline
    ];

    public TransactionController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? paymentMode, string? category)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Include(t => t.Member)
            .Include(t => t.SubMember)
            .AsQueryable();

        // PostgreSQL 'timestamp with time zone' requires UTC DateTime parameters (Kind=Utc).
        // We filter by whole days: [fromDate 00:00, toDate+1 00:00) in UTC.
        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(t => t.TransactionDate >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtcExclusive = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(t => t.TransactionDate < toUtcExclusive);
        }

        if (!string.IsNullOrWhiteSpace(paymentMode))
            query = query.Where(t => t.PaymentMode == paymentMode);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");
        ViewData["PaymentMode"] = paymentMode ?? string.Empty;
        ViewData["Category"] = category ?? string.Empty;
        ViewData["PaymentModes"] = new List<SelectListItem>
        {
            new() { Value = "", Text = "All" },
            new() { Value = Transaction.PaymentModeCash, Text = Transaction.PaymentModeCash },
            new() { Value = Transaction.PaymentModeOnline, Text = Transaction.PaymentModeOnline }
        };
        ViewData["Categories"] = new List<SelectListItem>
        {
            new() { Value = "", Text = "All" },
            new() { Value = Transaction.CategoryMemberRegistration, Text = Transaction.CategoryMemberRegistration },
            new() { Value = Transaction.CategorySubMemberRegistration, Text = Transaction.CategorySubMemberRegistration },
            new() { Value = Transaction.CategoryHalfYearlyContribution, Text = Transaction.CategoryHalfYearlyContribution },
            new() { Value = Transaction.CategoryDonation, Text = Transaction.CategoryDonation },
            new() { Value = Transaction.CategoryBankInterest, Text = Transaction.CategoryBankInterest },
            new() { Value = Transaction.CategoryBankCharges, Text = Transaction.CategoryBankCharges },
            new() { Value = Transaction.CategoryCashDepositToBank, Text = Transaction.CategoryCashDepositToBank },
            new() { Value = Transaction.CategoryBankWithdrawal, Text = Transaction.CategoryBankWithdrawal },
            new() { Value = Transaction.CategoryExpense, Text = Transaction.CategoryExpense }
        };

        return View(transactions);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateTransactionLists();
        return View(new Transaction
        {
            TransactionDate = DateTime.Today,
            Category = Transaction.CategoryExpense,
            Type = Transaction.TypeExpense,
            PaymentMode = Transaction.PaymentModeCash
        });
    }

    [HttpGet]
    public IActionResult ImportExcel()
    {
        ViewData["Title"] = "Import Transactions";
        ViewData["PageTitle"] = "Import Transactions";
        ViewData["BreadcrumbCurrent"] = "Import Transactions";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportExcel(IFormFile? excelFile)
    {
        if (excelFile == null || excelFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select an Excel file to import.";
            return RedirectToAction(nameof(ImportExcel));
        }

        if (!string.Equals(Path.GetExtension(excelFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only .xlsx files are supported for import.";
            return RedirectToAction(nameof(ImportExcel));
        }

        var loginUsername = HttpContext.Session.GetString("Username");
        var auditUser = string.IsNullOrWhiteSpace(loginUsername) ? "Unknown" : loginUsername.Trim();

        try
        {
            using var stream = excelFile.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                TempData["ErrorMessage"] = "The uploaded Excel file does not contain any worksheet.";
                return RedirectToAction(nameof(ImportExcel));
            }

            if (!TryReadHeaderMap(worksheet, out var headers))
            {
                TempData["ErrorMessage"] = "Invalid header row. Required columns: TransactionDate, Category, Amount, PaymentMode.";
                return RedirectToAction(nameof(ImportExcel));
            }

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null || usedRange.RowCount() <= 1)
            {
                TempData["ErrorMessage"] = "No transaction rows found in Excel file.";
                return RedirectToAction(nameof(ImportExcel));
            }

            var inserted = 0;
            var skipped = 0;
            var rowErrors = new List<string>();
            var lastRow = usedRange.LastRow().RowNumber();

            for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
            {
                var row = worksheet.Row(rowIndex);

                var dateRaw = GetCellValue(row, headers, "transactiondate");
                var categoryRaw = GetCellValue(row, headers, "category");
                var amountRaw = GetCellValue(row, headers, "amount");
                var paymentModeRaw = GetCellValue(row, headers, "paymentmode");
                var memberIdRaw = GetCellValue(row, headers, "memberid");
                var subMemberIdRaw = GetCellValue(row, headers, "submemberid");
                var donorName = GetCellValue(row, headers, "donorname");
                var donorMobile = GetCellValue(row, headers, "donormobile");
                var description = GetCellValue(row, headers, "description");

                var isCompletelyBlank =
                    string.IsNullOrWhiteSpace(dateRaw) &&
                    string.IsNullOrWhiteSpace(categoryRaw) &&
                    string.IsNullOrWhiteSpace(amountRaw) &&
                    string.IsNullOrWhiteSpace(paymentModeRaw) &&
                    string.IsNullOrWhiteSpace(memberIdRaw) &&
                    string.IsNullOrWhiteSpace(subMemberIdRaw) &&
                    string.IsNullOrWhiteSpace(donorName) &&
                    string.IsNullOrWhiteSpace(donorMobile) &&
                    string.IsNullOrWhiteSpace(description);

                if (isCompletelyBlank)
                    continue;

                if (!DateTime.TryParse(dateRaw, out var parsedDate))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: invalid TransactionDate.");
                    continue;
                }

                if (!decimal.TryParse(amountRaw, out var parsedAmount) || parsedAmount <= 0)
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: Amount must be greater than zero.");
                    continue;
                }

                var category = NormalizeValue(categoryRaw, AllowedCategories);
                if (category == null)
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: invalid Category.");
                    continue;
                }

                var paymentMode = NormalizeValue(paymentModeRaw, AllowedPaymentModes);
                if (paymentMode == null)
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: invalid PaymentMode.");
                    continue;
                }

                int? memberId = null;
                int? subMemberId = null;

                if (!string.IsNullOrWhiteSpace(memberIdRaw))
                {
                    if (!int.TryParse(memberIdRaw, out var parsedMemberId))
                    {
                        skipped++;
                        rowErrors.Add($"Row {rowIndex}: invalid MemberId.");
                        continue;
                    }

                    var memberExists = await _db.Members
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == parsedMemberId);
                    if (!memberExists)
                    {
                        skipped++;
                        rowErrors.Add($"Row {rowIndex}: MemberId {parsedMemberId} not found.");
                        continue;
                    }

                    memberId = parsedMemberId;
                }

                if (!string.IsNullOrWhiteSpace(subMemberIdRaw))
                {
                    if (!int.TryParse(subMemberIdRaw, out var parsedSubMemberId))
                    {
                        skipped++;
                        rowErrors.Add($"Row {rowIndex}: invalid SubMemberId.");
                        continue;
                    }

                    var subMemberExists = await _db.SubMembers
                        .AsNoTracking()
                        .AnyAsync(s => s.Id == parsedSubMemberId);
                    if (!subMemberExists)
                    {
                        skipped++;
                        rowErrors.Add($"Row {rowIndex}: SubMemberId {parsedSubMemberId} not found.");
                        continue;
                    }

                    subMemberId = parsedSubMemberId;
                }

                var tx = new Transaction
                {
                    TransactionDate = parsedDate,
                    Category = category,
                    Amount = parsedAmount,
                    PaymentMode = paymentMode,
                    MemberId = memberId,
                    SubMemberId = subMemberId,
                    DonorName = donorName,
                    DonorMobile = donorMobile,
                    Description = description,
                    CreatedBy = auditUser,
                    ModifiedBy = auditUser
                };

                ModelState.Clear();
                await ApplyBusinessRulesAsync(tx);
                if (!ModelState.IsValid)
                {
                    skipped++;
                    var firstError = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .FirstOrDefault();
                    rowErrors.Add($"Row {rowIndex}: {firstError ?? "validation failed."}");
                    continue;
                }

                _db.Transactions.Add(tx);
                inserted++;
            }

            if (inserted > 0)
                await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Import completed. New transactions inserted: {inserted}. Skipped rows: {skipped}.";
            if (rowErrors.Count > 0)
                TempData["ErrorMessage"] = string.Join(" ", rowErrors.Take(5)) + (rowErrors.Count > 5 ? " ..." : string.Empty);
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Failed to import Excel file. Please verify the file format and try again.";
        }

        return RedirectToAction(nameof(ImportExcel));
    }

    [HttpGet]
    public IActionResult DownloadImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Transactions");

        ws.Cell(1, 1).Value = "TransactionDate";
        ws.Cell(1, 2).Value = "Category";
        ws.Cell(1, 3).Value = "Amount";
        ws.Cell(1, 4).Value = "PaymentMode";
        ws.Cell(1, 5).Value = "MemberId";
        ws.Cell(1, 6).Value = "SubMemberId";
        ws.Cell(1, 7).Value = "DonorName";
        ws.Cell(1, 8).Value = "DonorMobile";
        ws.Cell(1, 9).Value = "Description";

        ws.Cell(2, 1).Value = DateTime.Today;
        ws.Cell(2, 2).Value = Transaction.CategoryDonation;
        ws.Cell(2, 3).Value = 500;
        ws.Cell(2, 4).Value = Transaction.PaymentModeOnline;
        ws.Cell(2, 7).Value = "Sample Donor";
        ws.Cell(2, 8).Value = "9876543210";
        ws.Cell(2, 9).Value = "Donation received through UPI";

        var headerRange = ws.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#e9f2ff");
        ws.Column(1).Style.DateFormat.Format = "yyyy-MM-dd";
        ws.Columns(1, 9).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "TransactionImportTemplate.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TransactionDate,Type,Category,MemberId,SubMemberId,DonorName,DonorMobile,Amount,PaymentMode,Description")] Transaction transaction)
    {
        await ApplyBusinessRulesAsync(transaction);

        if (!ModelState.IsValid)
        {
            await PopulateTransactionLists();
            return View(transaction);
        }

        var loginUsername = HttpContext.Session.GetString("Username");
        var auditUser = string.IsNullOrWhiteSpace(loginUsername) ? "Unknown" : loginUsername.Trim();
        transaction.CreatedBy = auditUser;
        transaction.ModifiedBy = auditUser;

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var transaction = await _db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (transaction == null)
            return NotFound();

        await PopulateTransactionLists();
        return View(transaction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TransactionDate,Category,MemberId,SubMemberId,DonorName,DonorMobile,Amount,PaymentMode,Description")] Transaction incoming)
    {
        if (id != incoming.Id)
            return NotFound();

        var entity = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null)
            return NotFound();

        entity.TransactionDate = incoming.TransactionDate;
        entity.Category = incoming.Category;
        entity.MemberId = incoming.MemberId;
        entity.SubMemberId = incoming.SubMemberId;
        entity.DonorName = incoming.DonorName;
        entity.DonorMobile = incoming.DonorMobile;
        entity.Amount = incoming.Amount;
        entity.PaymentMode = incoming.PaymentMode;
        entity.Description = incoming.Description ?? string.Empty;

        await ApplyBusinessRulesAsync(entity);

        if (!ModelState.IsValid)
        {
            await PopulateTransactionLists();
            return View(entity);
        }

        var loginUsername = HttpContext.Session.GetString("Username");
        var auditUser = string.IsNullOrWhiteSpace(loginUsername) ? "Unknown" : loginUsername.Trim();
        entity.ModifiedBy = auditUser;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var transaction = await _db.Transactions
            .AsNoTracking()
            .Include(t => t.Member)
            .Include(t => t.SubMember)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
            return NotFound();

        return View(transaction);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var transaction = await _db.Transactions.FindAsync(id);
        if (transaction != null)
        {
            _db.Transactions.Remove(transaction);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ApplyBusinessRulesAsync(Transaction transaction)
    {
        // Npgsql requires UTC for timestamp with time zone columns.
        transaction.TransactionDate = DateTime.SpecifyKind(transaction.TransactionDate.Date, DateTimeKind.Utc);

        transaction.DonorName = string.IsNullOrWhiteSpace(transaction.DonorName) ? null : transaction.DonorName.Trim();
        transaction.DonorMobile = string.IsNullOrWhiteSpace(transaction.DonorMobile) ? null : transaction.DonorMobile.Trim();
        transaction.Description = transaction.Description?.Trim() ?? string.Empty;

        var (contributionFee, registrationFee) = await GetFeesForDateAsync(transaction.TransactionDate);

        switch (transaction.Category)
        {
            case Transaction.CategoryMemberRegistration:
                transaction.Type = Transaction.TypeIncome;
                transaction.Amount = registrationFee;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (!transaction.MemberId.HasValue)
                    ModelState.AddModelError(nameof(Transaction.MemberId), "Member is required for member registration.");
                break;

            case Transaction.CategorySubMemberRegistration:
                transaction.Type = Transaction.TypeIncome;
                transaction.Amount = registrationFee;
                transaction.MemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (!transaction.SubMemberId.HasValue)
                    ModelState.AddModelError(nameof(Transaction.SubMemberId), "SubMember is required for sub-member registration.");
                break;

            case Transaction.CategoryHalfYearlyContribution:
                transaction.Type = Transaction.TypeIncome;
                if (transaction.Amount <= 0)
                    transaction.Amount = contributionFee;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (!transaction.MemberId.HasValue)
                    ModelState.AddModelError(nameof(Transaction.MemberId), "Member is required for half-yearly contribution.");
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Contribution amount must be greater than zero.");
                break;

            case Transaction.CategoryDonation:
                transaction.Type = Transaction.TypeIncome;
                transaction.MemberId = null;
                transaction.SubMemberId = null;
                if (string.IsNullOrWhiteSpace(transaction.DonorName))
                    ModelState.AddModelError(nameof(Transaction.DonorName), "Donor name is required for donation.");
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Donation amount must be greater than zero.");
                break;

            case Transaction.CategoryBankInterest:
                transaction.Type = Transaction.TypeIncome;
                transaction.MemberId = null;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Interest amount must be greater than zero.");
                break;

            case Transaction.CategoryBankCharges:
                transaction.Type = Transaction.TypeExpense;
                transaction.MemberId = null;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Bank charges amount must be greater than zero.");
                break;

            case Transaction.CategoryCashDepositToBank:
                transaction.Type = Transaction.TypeTransfer;
                transaction.MemberId = null;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Deposit amount must be greater than zero.");
                break;

            case Transaction.CategoryBankWithdrawal:
                transaction.Type = Transaction.TypeTransfer;
                transaction.MemberId = null;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Withdrawal amount must be greater than zero.");
                break;

            case Transaction.CategoryExpense:
                transaction.Type = Transaction.TypeExpense;
                transaction.MemberId = null;
                transaction.SubMemberId = null;
                transaction.DonorName = null;
                transaction.DonorMobile = null;
                if (transaction.Amount <= 0)
                    ModelState.AddModelError(nameof(Transaction.Amount), "Expense amount must be greater than zero.");
                break;

            default:
                ModelState.AddModelError(nameof(Transaction.Category), "Invalid category.");
                break;
        }

        if (transaction.PaymentMode is not (Transaction.PaymentModeCash or Transaction.PaymentModeOnline))
            ModelState.AddModelError(nameof(Transaction.PaymentMode), "Invalid payment mode.");
    }

    private async Task<(decimal ContributionFee, decimal RegistrationFee)> GetFeesForDateAsync(DateTime dateUtc)
    {
        var fee = await _db.FeeSettings
            .AsNoTracking()
            .Where(x => x.ApplyFromDate <= dateUtc)
            .OrderByDescending(x => x.ApplyFromDate)
            .FirstOrDefaultAsync();

        if (fee == null)
            return (200m, 50m);

        return (fee.ContributionFee, fee.RegistrationFee);
    }

    private async Task PopulateTransactionLists()
    {
        var members = await _db.Members
            .AsNoTracking()
            .OrderBy(m => m.Sr)
            .ThenBy(m => m.Name)
            .Select(m => new SelectListItem
            {
                Value = m.Id.ToString(),
                Text = $"{m.Sr} - {m.Name}"
            })
            .ToListAsync();

        var subMembers = await _db.SubMembers
            .AsNoTracking()
            .Include(s => s.Member)
            .OrderBy(s => s.SrNo)
            .ThenBy(s => s.Name)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"{s.SrNo} - {s.Name} ({(s.Member != null ? s.Member.Name : "No member")})"
            })
            .ToListAsync();

        ViewData["Members"] = members;
        ViewData["SubMembers"] = subMembers;
        ViewData["Categories"] = new List<SelectListItem>
        {
            new() { Value = Transaction.CategoryMemberRegistration, Text = Transaction.CategoryMemberRegistration },
            new() { Value = Transaction.CategorySubMemberRegistration, Text = Transaction.CategorySubMemberRegistration },
            new() { Value = Transaction.CategoryHalfYearlyContribution, Text = Transaction.CategoryHalfYearlyContribution },
            new() { Value = Transaction.CategoryDonation, Text = Transaction.CategoryDonation },
            new() { Value = Transaction.CategoryBankInterest, Text = Transaction.CategoryBankInterest },
            new() { Value = Transaction.CategoryBankCharges, Text = Transaction.CategoryBankCharges },
            new() { Value = Transaction.CategoryCashDepositToBank, Text = Transaction.CategoryCashDepositToBank },
            new() { Value = Transaction.CategoryBankWithdrawal, Text = Transaction.CategoryBankWithdrawal },
            new() { Value = Transaction.CategoryExpense, Text = Transaction.CategoryExpense }
        };
        ViewData["PaymentModes"] = new List<SelectListItem>
        {
            new() { Value = Transaction.PaymentModeCash, Text = Transaction.PaymentModeCash },
            new() { Value = Transaction.PaymentModeOnline, Text = Transaction.PaymentModeOnline }
        };
    }

    private static bool TryReadHeaderMap(IXLWorksheet worksheet, out Dictionary<string, int> headers)
    {
        headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = worksheet.Row(1);
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastCol == 0)
            return false;

        for (var col = 1; col <= lastCol; col++)
        {
            var raw = headerRow.Cell(col).GetValue<string>();
            var key = NormalizeHeader(raw);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            headers[key] = col;
        }

        return headers.ContainsKey("transactiondate")
            && headers.ContainsKey("category")
            && headers.ContainsKey("amount")
            && headers.ContainsKey("paymentmode");
    }

    private static string GetCellValue(IXLRow row, IReadOnlyDictionary<string, int> headers, string headerKey)
    {
        if (!headers.TryGetValue(headerKey, out var col))
            return string.Empty;

        return row.Cell(col).GetValue<string>().Trim();
    }

    private static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var chars = header.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static string? NormalizeValue(string? raw, IReadOnlyCollection<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return allowedValues.FirstOrDefault(v => string.Equals(v, raw.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
