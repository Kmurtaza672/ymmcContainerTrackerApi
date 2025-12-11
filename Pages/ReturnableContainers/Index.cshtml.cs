using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(AppDbContext context, IUserService userService, ILogger<IndexModel> logger)
        {
            _context = context;
            _userService = userService;
            _logger = logger;
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
                _logger.LogWarning("User {CurrentUser} attempted to view containers without permission", currentUser);
                TempData["ErrorMessage"] = "You do not have permission to view containers.";
                return RedirectToPage("/Index");
            }

            _logger.LogInformation("User {CurrentUser} accessed containers (CanEdit: {CanEdit})", currentUser, CanEdit);


            // Guard against legacy rows with NULL Item_No to avoid SqlNullValueException
            ReturnableContainers = await _context.ReturnableContainers
                .AsNoTracking()
                .Where(rc => rc.ItemNo != null)
                .ToListAsync();

            return Page();
        }

        // GET: return edit-mode row
        public async Task<PartialViewResult> OnGetEditRowAsync(string id)
        {
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("❌ User {CurrentUser} attempted to edit without permission", currentUser);
                // Return read-only row
                var item = await _context.ReturnableContainers.AsNoTracking().FirstOrDefaultAsync(x => x.ItemNo == id);
                return Partial("_Row", new ReturnableContainersRowModel { Item = item ?? new ReturnableContainers { ItemNo = id }, IsEditing = false });
            }

            var editItem = await _context.ReturnableContainers.AsNoTracking().FirstOrDefaultAsync(x => x.ItemNo == id);
            if (editItem == null)
            {
                return Partial("_Row", new ReturnableContainersRowModel { Item = new ReturnableContainers { ItemNo = id }, IsEditing = false });
            }
            return Partial("_Row", new ReturnableContainersRowModel { Item = editItem, IsEditing = true });
        }

        // POST: save inline edit
        public async Task<PartialViewResult> OnPostSaveRowAsync([FromForm] ReturnableContainers item)
        {
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("❌ BLOCKED: User {CurrentUser} with role Viewer attempted to save changes", currentUser);
                // Return read-only row (block the save)
                var existingItem = await _context.ReturnableContainers.AsNoTracking().FirstOrDefaultAsync(x => x.ItemNo == item.ItemNo);
                return Partial("_Row", new ReturnableContainersRowModel { Item = existingItem ?? item, IsEditing = false });
            }

            var existing = await _context.ReturnableContainers.FirstOrDefaultAsync(x => x.ItemNo == item.ItemNo);
            if (existing == null)
            {
                return Partial("_Row", new ReturnableContainersRowModel { Item = item, IsEditing = false });
            }

            var packing = (item.PackingCode ?? string.Empty).Trim();
            var prefix = (item.PrefixCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(packing) || string.IsNullOrWhiteSpace(prefix))
            {
                return Partial("_Row", new ReturnableContainersRowModel { Item = item, IsEditing = true });
            }

            existing.PackingCode = packing;
            existing.PrefixCode = prefix;
            existing.ContainerNumber = (item.ContainerNumber ?? string.Empty).Trim();
            existing.OutsideLength = item.OutsideLength;
            existing.OutsideWidth = item.OutsideWidth;
            existing.OutsideHeight = item.OutsideHeight;
            existing.CollapsedHeight = item.CollapsedHeight;
            existing.Weight = item.Weight;
            existing.PackQuantity = item.PackQuantity;
            existing.AlternateId = (item.AlternateId ?? string.Empty).Trim();

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ User {CurrentUser} successfully updated container {ItemNo}", currentUser, item.ItemNo);

            return Partial("_Row", new ReturnableContainersRowModel { Item = existing, IsEditing = false });
        }
    }
}
