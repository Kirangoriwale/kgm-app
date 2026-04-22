using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class FeeSetting
{
    public int Id { get; set; }

    [Display(Name = "Apply From Date")]
    [DataType(DataType.Date)]
    public DateTime ApplyFromDate { get; set; }

    [Display(Name = "Contribution Fee")]
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal ContributionFee { get; set; }

    [Display(Name = "Registration Fee")]
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal RegistrationFee { get; set; }
}

