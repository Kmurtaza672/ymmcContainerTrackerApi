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
using YmmcContainerTrackerApi.Services; // ✅ ADD THIS

namespace YmmcContainerTrackerApi.Pages_ReturnableContainers
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService; // ✅ ADD THIS
        private readonly ILogger<EditModel> _logger; // ✅ ADD THIS

        // ✅ UPDATE CONSTRUCTOR
        public EditModel(AppDbContext context, IUserService userService, ILogger<EditModel> logger)
        {
            _context = context;
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            // ✅ CHECK EDIT PERMISSION
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("❌ User {CurrentUser} attempted to access Edit page without permission", currentUser);
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
            // ✅ CHECK EDIT PERMISSION AGAIN (prevent direct POST attacks)
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("❌ BLOCKED: User {CurrentUser} attempted to POST edit without permission", currentUser);
                TempData["ErrorMessage"] = "You do not have permission to edit containers.";
                return RedirectToPage("./Index");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(ReturnableContainers).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ User {CurrentUser} edited container {ItemNo}", currentUser, ReturnableContainers.ItemNo);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReturnableContainersExists(ReturnableContainers.ItemNo))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ReturnableContainersExists(string id)
        {
            return _context.ReturnableContainers.Any(e => e.ItemNo == id);
        }
    }
}
