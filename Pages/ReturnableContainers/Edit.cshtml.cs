using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            AppDbContext context,
            IUserService userService,
            IAuditService auditService,
            ILogger<EditModel> logger)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
            _logger = logger;
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            // CHECK EDIT PERMISSION
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("BLOCKED: User {CurrentUser} attempted to access Edit page without permission", currentUser);
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
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // CHECK EDIT PERMISSION AGAIN (prevent direct POST attacks)
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("BLOCKED: User {CurrentUser} attempted to POST edit without permission", currentUser);
                TempData["ErrorMessage"] = "You do not have permission to edit containers.";
                return RedirectToPage("./Index");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Get the old values BEFORE updating
            var oldContainer = await _context.ReturnableContainers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ItemNo == ReturnableContainers.ItemNo);

            if (oldContainer == null)
            {
                return NotFound();
            }

            // START TRANSACTION - Ensure atomic operation
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Attach(ReturnableContainers).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Log the UPDATE action with old and new values
                await _auditService.LogUpdateAsync(
                    ReturnableContainers.ItemNo,
                    oldContainer,
                    ReturnableContainers,
                    currentUser);

                // COMMIT - Both update and audit log succeed together
                await transaction.CommitAsync();

                _logger.LogInformation("SUCCESS: User {CurrentUser} edited container {ItemNo}", currentUser, ReturnableContainers.ItemNo);
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
                    return RedirectToPage("./Edit", new { id = ReturnableContainers.ItemNo });
                }

                throw;
            }
            catch (Exception ex)
            {
                // ROLLBACK on any error - Nothing gets saved
                await transaction.RollbackAsync();
                _logger.LogError(ex, "ERROR: Failed to edit container {ItemNo}", ReturnableContainers.ItemNo);
                TempData["ErrorMessage"] = "An error occurred while updating the container. Please try again.";
                return Page();
            }
        }

        private bool ReturnableContainersExists(string id)
        {
            return _context.ReturnableContainers.Any(e => e.ItemNo == id);
        }
    }
}
