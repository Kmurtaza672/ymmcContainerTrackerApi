using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YmmcContainerTrackerApi.Data;
using YmmcContainerTrackerApi.Models;
using YmmcContainerTrackerApi.Services;

namespace YmmcContainerTrackerApi.Pages_ReturnableContainers
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;

        public EditModel(
            AppDbContext context,
            IUserService userService,
            IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        [BindProperty]
        public string OriginalItemNo { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            // CHECK EDIT PERMISSION
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                TempData["ErrorMessage"] = "You do not have permission to edit containers.";
                return RedirectToPage("./Index");
            }

            if (id == null)
            {
                return NotFound();
            }

            var returnablecontainers = await _context.ReturnableContainers.FirstOrDefaultAsync(m => m.ItemNo == id);
            if (returnablecontainers == null)
            {
                return NotFound();
            }
            
            ReturnableContainers = returnablecontainers;
            OriginalItemNo = returnablecontainers.ItemNo; // Store original for comparison
            
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // CHECK EDIT PERMISSION AGAIN (prevent direct POST attacks)
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                TempData["ErrorMessage"] = "You do not have permission to edit containers.";
                return RedirectToPage("./Index");
            }

            // Normalize standard fields (uppercase for consistency)
            string Normalize(string? value)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length == 0) return string.Empty;
                trimmed = Regex.Replace(trimmed, "\\s+", " ");
                return trimmed.ToUpperInvariant();
            }

            // normalization for ItemNo only capitalize the prefix
            string NormalizeItemNo(string? itemNo)
            {
                var trimmed = (itemNo ?? string.Empty).Trim();
                if (trimmed.Length < 3) return trimmed;
                
                // Capitalize only the first 3 characters (prefix)
                var prefix = trimmed.Substring(0, 3).ToUpperInvariant();
                var rest = trimmed.Substring(3);
                
                return prefix + rest;
            }

            ReturnableContainers.ItemNo = NormalizeItemNo(ReturnableContainers.ItemNo);
            ReturnableContainers.PackingCode = Normalize(ReturnableContainers.PackingCode);
            ReturnableContainers.PrefixCode = Normalize(ReturnableContainers.PrefixCode);

            // Re-validate normalized ItemNo
            ModelState.Remove("ReturnableContainers.ItemNo");
            if (!TryValidateModel(ReturnableContainers, "ReturnableContainers"))
            {
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check if ItemNo was changed
            bool itemNoChanged = !string.Equals(OriginalItemNo, ReturnableContainers.ItemNo, StringComparison.OrdinalIgnoreCase);

            if (itemNoChanged)
            {
                // Check if new ItemNo already exists (prevent duplicates)
                var exists = await _context.ReturnableContainers
                    .AsNoTracking()
                    .AnyAsync(rc => rc.ItemNo.ToUpper() == ReturnableContainers.ItemNo.ToUpper());

                if (exists)
                {
                    ModelState.AddModelError("ReturnableContainers.ItemNo", "This Item No already exists.");
                    return Page();
                }
            }

            // Get the old values BEFORE updating
            var oldContainer = await _context.ReturnableContainers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ItemNo == OriginalItemNo);

            if (oldContainer == null)
            {
                TempData["ErrorMessage"] = "The container was not found. It may have been deleted.";
                return RedirectToPage("./Index");
            }

            // START TRANSACTION - Ensure atomic operation
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (itemNoChanged)
                {
                    // ItemNo changed - We need to DELETE old record and CREATE new one
                    // because ItemNo is the primary key
                    
                    // 1. Remove old container
                    _context.ReturnableContainers.Remove(oldContainer);
                    await _context.SaveChangesAsync();

                    // 2. Add new container with new ItemNo
                    _context.ReturnableContainers.Add(ReturnableContainers);
                    await _context.SaveChangesAsync();

                    // 3. Log the ItemNo change as an UPDATE with special note
                    await _auditService.LogUpdateAsync(
                        ReturnableContainers.ItemNo,
                        oldContainer,
                        ReturnableContainers,
                        currentUser);
                }
                else
                {
                    // ItemNo unchanged - Normal update
                    _context.Attach(ReturnableContainers).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Log the UPDATE action
                    await _auditService.LogUpdateAsync(
                        ReturnableContainers.ItemNo,
                        oldContainer,
                        ReturnableContainers,
                        currentUser);
                }

                // COMMIT - Both update and audit log succeed together
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Container {ReturnableContainers.ItemNo} successfully updated.";
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();

                var entry = ex.Entries.SingleOrDefault();
                if (entry != null)
                {
                    var databaseValues = await entry.GetDatabaseValuesAsync();

                    if (databaseValues == null)
                    {
                        TempData["ErrorMessage"] = "This container was deleted by another user.";
                        return RedirectToPage("./Index");
                    }

                    TempData["ErrorMessage"] = "This container was modified by another user. Please refresh and try again.";
                    return RedirectToPage("./Edit", new { id = OriginalItemNo });
                }

                throw;
            }
            catch (Exception ex)
            {
                // ROLLBACK on any error - Nothing gets saved
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "An error occurred while updating the container. Please try again.";
                return Page();
            }
        }
    }
}
