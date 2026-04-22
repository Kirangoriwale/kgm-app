using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class TransactionController : Controller
{
    private readonly AppDbContext _db;

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
}
