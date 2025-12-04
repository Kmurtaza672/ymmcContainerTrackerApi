using System.ComponentModel.DataAnnotations;

namespace YmmcContainerTrackerApi.Models;

public partial class ReturnableContainers
{
    [Key]
    [StringLength(50)]
    public string ItemNo { get; set; } = string.Empty; // PK must be non-null

    // Make strings nullable to tolerate existing NULLs in DB rows
    [StringLength(50)]
    public string? PackingCode { get; set; }

    [StringLength(50)]
    public string? PrefixCode { get; set; }

    [StringLength(100)]
    public string? ContainerNumber { get; set; }

    // optional fields below...
    public decimal? OutsideLength { get; set; }
    public decimal? OutsideWidth { get; set; }
    public decimal? OutsideHeight { get; set; }
    public decimal? CollapsedHeight { get; set; }
    public decimal? Weight { get; set; }
    public int? PackQuantity { get; set; }
    public string? AlternateId { get; set; }
}
