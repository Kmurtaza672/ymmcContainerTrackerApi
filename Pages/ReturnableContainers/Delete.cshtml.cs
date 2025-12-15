using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using YmmcContainerTrackerApi.Data;
using YmmcContainerTrackerApi.Models;
using YmmcContainerTrackerApi.Services;

namespace YmmcContainerTrackerApi.Pages_ReturnableContainers
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;
        private readonly ILogger<DeleteModel> _logger;

        public DeleteModel(
            AppDbContext context,
            IUserService userService,
            IAuditService auditService,
            ILogger<DeleteModel> logger)
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
            // CHECK DELETE PERMISSION (only Admin can delete)
            var currentUser = _userService.GetCurrentUsername();
            var role = await _userService.GetUserRoleAsync(currentUser);

            if (role != "Admin")
            {
                _logger.LogWarning("BLOCKED: User {CurrentUser} with role {Role} attempted to delete without permission", currentUser, role);
                TempData["ErrorMessage"] = "You do not have permission to delete containers. Only Admins can delete.";
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

        public async Task<IActionResult> OnPostAsync(string id)
        {
            // CHECK DELETE PERMISSION AGAIN
            var currentUser = _userService.GetCurrentUsername();
            var role = await _userService.GetUserRoleAsync(currentUser);

            if (role != "Admin")
            {
                _logger.LogWarning("BLOCKED: User {CurrentUser} with role {Role} attempted to POST delete", currentUser, role);
                TempData["ErrorMessage"] = "You do not have permission to delete containers.";
                return RedirectToPage("./Index");
            }

            if (id == null)
            {
                return NotFound();
            }

            // START TRANSACTION - Ensure atomic operation
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var returnablecontainers = await _context.ReturnableContainers.FindAsync(id);

                if (returnablecontainers == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                // Log the delete action FIRST (inside transaction)
                await _auditService.LogDeleteAsync(id, returnablecontainers, currentUser);

                // Then delete the container
                ReturnableContainers = returnablecontainers;
                _context.ReturnableContainers.Remove(ReturnableContainers);
                await _context.SaveChangesAsync();

                // COMMIT - Both audit log and delete succeed together
                await transaction.CommitAsync();

                _logger.LogInformation("SUCCESS: Admin {CurrentUser} deleted container {ItemNo}", currentUser, id);
                TempData["SuccessMessage"] = $"Container {id} successfully deleted.";
            }
            catch (Exception ex)
            {
                // ROLLBACK on any error - Nothing gets saved
                await transaction.RollbackAsync();
                _logger.LogError(ex, "ERROR: Failed to delete container {ItemNo}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the container. Please try again.";
            }

            return RedirectToPage("./Index");
        }
    }
}
