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
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;
        private readonly ILogger<DeleteModel> _logger; 

        // ✅ UPDATE CONSTRUCTOR
        public DeleteModel(AppDbContext context, IUserService userService, ILogger<DeleteModel> logger)
        {
            _context = context;
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            // ✅ CHECK DELETE PERMISSION (only Admin can delete)
            var currentUser = _userService.GetCurrentUsername();
            var role = await _userService.GetUserRoleAsync(currentUser);

            if (role != "Admin")
            {
                _logger.LogWarning("❌ User {CurrentUser} with role {Role} attempted to delete without permission", currentUser, role);
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
            else
            {
                ReturnableContainers = returnablecontainers;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            // ✅ CHECK DELETE PERMISSION AGAIN (防止直接POST攻击)
            var currentUser = _userService.GetCurrentUsername();
            var role = await _userService.GetUserRoleAsync(currentUser);

            if (role != "Admin")
            {
                _logger.LogWarning("❌ BLOCKED: User {CurrentUser} with role {Role} attempted to POST delete", currentUser, role);
                TempData["ErrorMessage"] = "You do not have permission to delete containers.";
                return RedirectToPage("./Index");
            }

            if (id == null)
            {
                return NotFound();
            }

            var returnablecontainers = await _context.ReturnableContainers.FindAsync(id);
            if (returnablecontainers != null)
            {
                ReturnableContainers = returnablecontainers;
                _context.ReturnableContainers.Remove(ReturnableContainers);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("✅ Admin {CurrentUser} deleted container {ItemNo}", currentUser, id);
            }

            return RedirectToPage("./Index");
        }
    }
}
