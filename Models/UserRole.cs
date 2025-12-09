using System.ComponentModel.DataAnnotations;

namespace YmmcContainerTrackerApi.Models; // Changed from .Services to .Models

public class UserRole
{
    [Key]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty; // (without domain)

    [StringLength(20)]
    public string Role { get; set; } = string.Empty; // "Editor", "Viewer", "Admin"

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastModified { get; set; }

    [StringLength(100)]
    public string? DisplayName { get; set; } //firstname lastname

    [StringLength(100)]
    public string? Email { get; set; }  //kmurtaza@company.com
}
