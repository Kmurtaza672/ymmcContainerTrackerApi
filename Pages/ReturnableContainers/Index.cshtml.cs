using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using YmmcContainerTrackerApi.Data;
using YmmcContainerTrackerApi.Models;
using YmmcContainerTrackerApi.Services;

namespace YmmcContainerTrackerApi.Pages_ReturnableContainers
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public IndexModel(
            AppDbContext context,
            IUserService userService,
            IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        public IList<ReturnableContainers> ReturnableContainers { get; set; } = default!;

        public bool CanEdit { get; set; }
        public bool CanView { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var currentUser = _userService.GetCurrentUsername();
            CanView = await _userService.CanViewAsync(currentUser);
            CanEdit = await _userService.CanEditAsync(currentUser);

            if (!CanView)
            {
                TempData["ErrorMessage"] = "You do not have permission to view containers.";
                return RedirectToPage("/Index");
            }

            // Guard against legacy rows with NULL Item_No to avoid SqlNullValueException
            ReturnableContainers = await _context.ReturnableContainers
                .AsNoTracking()
                .Where(rc => rc.ItemNo != null)
                .ToListAsync();

            return Page();
        }

        // GET: return edit-mode row (or view-mode if isEditing=false)
        public async Task<PartialViewResult> OnGetEditRowAsync(string id, bool isEditing = true)
        {
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                // Return read-only row
                var item = await _context.ReturnableContainers.AsNoTracking().FirstOrDefaultAsync(x => x.ItemNo == id);
                return Partial("_Row", new ReturnableContainersRowModel { Item = item ?? new ReturnableContainers { ItemNo = id }, IsEditing = false, OriginalItemNo = id });
            }

            var editItem = await _context.ReturnableContainers.AsNoTracking().FirstOrDefaultAsync(x => x.ItemNo == id);
            if (editItem == null)
            {
                return Partial("_Row", new ReturnableContainersRowModel { Item = new ReturnableContainers { ItemNo = id }, IsEditing = false, OriginalItemNo = id });
            }
            return Partial("_Row", new ReturnableContainersRowModel { Item = editItem, IsEditing = isEditing, OriginalItemNo = editItem.ItemNo });
        }

        // POST: save inline edit with ItemNo change support
        public async Task<PartialViewResult> OnPostSaveRowAsync([FromForm] ReturnableContainers item, [FromForm] string OriginalItemNo)
        {
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                var existingItem = await _context.ReturnableContainers.AsNoTracking().FirstOrDefaultAsync(x => x.ItemNo == OriginalItemNo);
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = existingItem ?? item, 
                    IsEditing = false,
                    OriginalItemNo = OriginalItemNo 
                });
            }

            string Normalize(string? value)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length == 0) return string.Empty;
                trimmed = Regex.Replace(trimmed, "\\s+", " ");
                return trimmed.ToUpperInvariant();
            }

            // Special normalization for ItemNo - only capitalize the prefix
            string NormalizeItemNo(string? itemNo)
            {
                var trimmed = (itemNo ?? string.Empty).Trim();
                if (trimmed.Length < 3) return trimmed;
                
                // Capitalize only the first 3 characters (prefix)
                var prefix = trimmed.Substring(0, 3).ToUpperInvariant();
                var rest = trimmed.Substring(3);
                
                return prefix + rest;
            }

            // Normalize inputs
            item.ItemNo = NormalizeItemNo(item.ItemNo);
            item.PackingCode = Normalize(item.PackingCode);
            item.PrefixCode = Normalize(item.PrefixCode);
            item.ContainerNumber = (item.ContainerNumber ?? string.Empty).Trim();
            item.AlternateId = (item.AlternateId ?? string.Empty).Trim();

            // Validate required fields with specific error messages
            if (string.IsNullOrWhiteSpace(item.ItemNo))
            {
                // Store original ItemNo in the item for cancel functionality
                item.ItemNo = OriginalItemNo;
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = item, 
                    IsEditing = true,
                    ErrorMessage = "Item No is required and cannot be empty.",
                    OriginalItemNo = OriginalItemNo
                });
            }

            if (string.IsNullOrWhiteSpace(item.PackingCode))
            {
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = item, 
                    IsEditing = true,
                    ErrorMessage = "Packing Code is required.",
                    OriginalItemNo = OriginalItemNo  
                });
            }

            if (string.IsNullOrWhiteSpace(item.PrefixCode))
            {
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = item, 
                    IsEditing = true,
                    ErrorMessage = "Prefix Code is required.",
                    OriginalItemNo = OriginalItemNo  
                });
            }

            var itemNoRegex = new Regex(@"^[A-Z]{3}-[A-Za-z0-9]+(?:[-xX][A-Za-z0-9]+)*$");
            if (!itemNoRegex.IsMatch(item.ItemNo))
            {
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = item, 
                    IsEditing = true,
                    ErrorMessage = "Item No must start with 3 uppercase letters. Examples: YPT-2415-07, YPB-4845-34",
                    OriginalItemNo = OriginalItemNo  
                });
            }

            var existing = await _context.ReturnableContainers.FirstOrDefaultAsync(x => x.ItemNo == OriginalItemNo);
            if (existing == null)
            {
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = item, 
                    IsEditing = false,
                    OriginalItemNo = OriginalItemNo
                });
            }

            bool itemNoChanged = !string.Equals(OriginalItemNo, item.ItemNo, StringComparison.OrdinalIgnoreCase);

            if (itemNoChanged)
            {
                var exists = await _context.ReturnableContainers
                    .AsNoTracking()
                    .AnyAsync(rc => rc.ItemNo.ToUpper() == item.ItemNo.ToUpper());

                if (exists)
                {
                    return Partial("_Row", new ReturnableContainersRowModel 
                    { 
                        Item = item, 
                        IsEditing = true,
                        ErrorMessage = "This Item No already exists. Please use a different Item No.",
                        OriginalItemNo = OriginalItemNo  
                    });
                }
            }

            var oldContainer = new ReturnableContainers
            {
                ItemNo = existing.ItemNo,
                PackingCode = existing.PackingCode,
                PrefixCode = existing.PrefixCode,
                ContainerNumber = existing.ContainerNumber,
                OutsideLength = existing.OutsideLength,
                OutsideWidth = existing.OutsideWidth,
                OutsideHeight = existing.OutsideHeight,
                CollapsedHeight = existing.CollapsedHeight,
                Weight = existing.Weight,
                PackQuantity = existing.PackQuantity,
                AlternateId = existing.AlternateId
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (itemNoChanged)
                {
                    // ItemNo changed - We need to delete old record and create a new one
                    // because ItemNo is the primary key
                    
                    // Remove old container
                    _context.ReturnableContainers.Remove(existing);
                    await _context.SaveChangesAsync();

                    // Add new container with new ItemNo and all updated values
                    var newContainer = new ReturnableContainers
                    {
                        ItemNo = item.ItemNo,
                        PackingCode = item.PackingCode,
                        PrefixCode = item.PrefixCode,
                        ContainerNumber = item.ContainerNumber,
                        OutsideLength = item.OutsideLength,
                        OutsideWidth = item.OutsideWidth,
                        OutsideHeight = item.OutsideHeight,
                        CollapsedHeight = item.CollapsedHeight,
                        Weight = item.Weight,
                        PackQuantity = item.PackQuantity,
                        AlternateId = item.AlternateId
                    };

                    _context.ReturnableContainers.Add(newContainer);
                    await _context.SaveChangesAsync();
                    await _auditService.LogUpdateAsync(newContainer.ItemNo, oldContainer, newContainer, currentUser);
                    await transaction.CommitAsync();

                    return Partial("_Row", new ReturnableContainersRowModel 
                    { 
                        Item = newContainer, 
                        IsEditing = false,
                        OriginalItemNo = newContainer.ItemNo  // New ItemNo becomes the original
                    });
                }
                else
                {
                    // ItemNo unchanged - Normal update
                    existing.PackingCode = item.PackingCode;
                    existing.PrefixCode = item.PrefixCode;
                    existing.ContainerNumber = item.ContainerNumber;
                    existing.OutsideLength = item.OutsideLength;
                    existing.OutsideWidth = item.OutsideWidth;
                    existing.OutsideHeight = item.OutsideHeight;
                    existing.CollapsedHeight = item.CollapsedHeight;
                    existing.Weight = item.Weight;
                    existing.PackQuantity = item.PackQuantity;
                    existing.AlternateId = item.AlternateId;

                    await _context.SaveChangesAsync();
                    await _auditService.LogUpdateAsync(item.ItemNo, oldContainer, existing, currentUser);
                    await transaction.CommitAsync();

                    return Partial("_Row", new ReturnableContainersRowModel 
                    { 
                        Item = existing, 
                        IsEditing = false,
                        OriginalItemNo = existing.ItemNo  // Add this
                    });
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = item, 
                    IsEditing = true,
                    ErrorMessage = "An error occurred while saving. Please try again.",
                    OriginalItemNo = OriginalItemNo
                });
            }
        }

        // GET handler for Cancel button - reads originalId from query or form
        public async Task<PartialViewResult> OnGetCancelEditAsync([FromQuery] string? originalId)
        {
            // Use the original ItemNo from hidden field
            if (string.IsNullOrWhiteSpace(originalId))
            {
                // If originalId is empty, return an error state
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = new ReturnableContainers(), 
                    IsEditing = false,
                    ErrorMessage = "Unable to cancel: Original Item No not found."
                });
            }

            var item = await _context.ReturnableContainers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ItemNo == originalId);

            if (item == null)
            {
                // Item doesn't exist
                return Partial("_Row", new ReturnableContainersRowModel 
                { 
                    Item = new ReturnableContainers { ItemNo = originalId }, 
                    IsEditing = false,
                    ErrorMessage = $"Container '{originalId}' not found in database."
                });
            }

            // Successfully return the original row in view mode
            return Partial("_Row", new ReturnableContainersRowModel 
            { 
                Item = item, 
                IsEditing = false 
            });
        }
    }
}
