using YmmcContainerTrackerApi.Models;

namespace YmmcContainerTrackerApi.Models
{
    // Row view-model for inline editing partial
    public class ReturnableContainersRowModel
    {
        public ReturnableContainers Item { get; set; } = new();
        public bool IsEditing { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Store the original ItemNo from database (before any user modifications)
        public string? OriginalItemNo { get; set; }
    }
}
