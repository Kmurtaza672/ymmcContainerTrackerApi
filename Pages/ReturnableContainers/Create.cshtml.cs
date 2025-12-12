using System.Text.RegularExpressions;
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
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;
        private readonly ILogger<CreateModel> _logger;

        //UPDATE CONSTRUCTOR
        public CreateModel(AppDbContext context, IUserService userService, IAuditService auditService, ILogger<CreateModel> logger)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            //CHECK CREATE PERMISSION
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = _userService.CanEditAsync(currentUser).Result;

            if (!canEdit)
            {
                _logger.LogWarning("❌ User {CurrentUser} attempted to access Create page without permission", currentUser);
                TempData["ErrorMessage"] = "You do not have permission to create containers.";
                return RedirectToPage("./Index");
            }

            return Page();
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        public async Task<IActionResult> OnPostAsync()
        {
            //CHECK CREATE PERMISSION (prevent direct POST attacks)
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                _logger.LogWarning("❌ BLOCKED: User {CurrentUser} attempted to POST create without permission", currentUser);
                TempData["ErrorMessage"] = "You do not have permission to create containers.";
                return RedirectToPage("./Index");
            }

            // Normalize key fields before validation
            string Normalize(string? s)
            {
                var trimmed = (s ?? string.Empty).Trim();
                if (trimmed.Length == 0) return string.Empty;
                // collapse internal whitespace to single space
                trimmed = Regex.Replace(trimmed, "\\s+", " ");
                // uppercase for consistency
                return trimmed.ToUpperInvariant();
            }

            ReturnableContainers.ItemNo = Normalize(ReturnableContainers.ItemNo);
            ReturnableContainers.PackingCode = Normalize(ReturnableContainers.PackingCode);
            ReturnableContainers.PrefixCode = Normalize(ReturnableContainers.PrefixCode);

            if (string.IsNullOrWhiteSpace(ReturnableContainers.ItemNo))
                ModelState.AddModelError("ReturnableContainers.ItemNo", "Please add the column [Item_No].");
            if (string.IsNullOrWhiteSpace(ReturnableContainers.PackingCode))
                ModelState.AddModelError("ReturnableContainers.PackingCode", "Please add the column [Packing_Code].");
            if (string.IsNullOrWhiteSpace(ReturnableContainers.PrefixCode))
                ModelState.AddModelError("ReturnableContainers.PrefixCode", "Please add the column [Prefix_Code].");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check for duplicates (by normalized ItemNo)
            var exists = await _context.ReturnableContainers
                .AsNoTracking()
                .AnyAsync(rc => rc.ItemNo == ReturnableContainers.ItemNo);

            if (exists)
            {
                ModelState.AddModelError("ReturnableContainers.ItemNo", "This Item No already exists.");
                return Page();
            }

            //  START TRANSACTION - Ensure atomic operation
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create the container
                _context.ReturnableContainers.Add(ReturnableContainers);
                await _context.SaveChangesAsync();

                // Log the CREATE action
                await _auditService.LogCreateAsync(ReturnableContainers.ItemNo, ReturnableContainers, currentUser);

                //  COMMIT - Both create and audit log succeed together
                await transaction.CommitAsync();

                _logger.LogInformation("✅ User {CurrentUser} created new container {ItemNo}", currentUser, ReturnableContainers.ItemNo);
                TempData["SuccessMessage"] = $"Container {ReturnableContainers.ItemNo} successfully created.";

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                //  ROLLBACK on any error - Nothing gets saved
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Failed to create container {ItemNo}", ReturnableContainers.ItemNo);
                TempData["ErrorMessage"] = "An error occurred while creating the container. Please try again.";
                return Page();
            }
        }
    }
}
