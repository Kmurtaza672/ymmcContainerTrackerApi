using System.ComponentModel.DataAnnotations;

namespace YmmcContainerTrackerApi.Models;

public partial class ReturnableContainers
{
    [Key]
    [Required(ErrorMessage = "Item No is required")]
    [StringLength(15, ErrorMessage = "Item No cannot exceed 15 characters")]
    [RegularExpression(@"^[A-Z]{3}-\d{4}-\d{2}$",
        ErrorMessage = "Item No must follow format: YPT-1207-05")]
    public string ItemNo { get; set; } = string.Empty;

    [StringLength(15, ErrorMessage = "Packing Code cannot exceed 15 characters")]
    public string? PackingCode { get; set; }

    [StringLength(15, ErrorMessage = "Prefix Code cannot exceed 15 characters")]
    public string? PrefixCode { get; set; }

    [StringLength(15, ErrorMessage = "Container Number cannot exceed 15 characters")]
    public string? ContainerNumber { get; set; }

    // Dimension fields with unit labels
    [Display(Name = "Outside Length (inches)")]
    [Range(0.01, 9999.99, ErrorMessage = "Outside Length must be between 0.01 and 9999.99 inches")]
    public decimal? OutsideLength { get; set; }

    [Display(Name = "Outside Width (inches)")]
    [Range(0.01, 9999.99, ErrorMessage = "Outside Width must be between 0.01 and 9999.99 inches")]
    public decimal? OutsideWidth { get; set; }

    [Display(Name = "Outside Height (inches)")]
    [Range(0.01, 9999.99, ErrorMessage = "Outside Height must be between 0.01 and 9999.99 inches")]
    public decimal? OutsideHeight { get; set; }

    [Display(Name = "Collapsed Height (inches)")]
    [Range(0.01, 9999.99, ErrorMessage = "Collapsed Height must be between 0.01 and 9999.99 inches")]
    public decimal? CollapsedHeight { get; set; }

    [Display(Name = "Weight (lbs)")]
    [Range(0.01, 99999.99, ErrorMessage = "Weight must be between 0.01 and 99999.99 lbs")]
    public decimal? Weight { get; set; }

    [Display(Name = "Pack Quantity")]
    [Range(1, 99999, ErrorMessage = "Pack Quantity must be between 1 and 99999")]
    public int? PackQuantity { get; set; }

    [StringLength(15, ErrorMessage = "Alternate ID cannot exceed 15 characters")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Alternate ID can only contain letters and numbers")]
    [Display(Name = "Alternate ID")]
    public string? AlternateId { get; set; }
}
