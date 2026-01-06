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
        

        public CreateModel(AppDbContext context, IUserService userService, IAuditService auditService)
        {
            _context = context;
            _userService = userService;
            _auditService = auditService;
            
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // CHECK CREATE PERMISSION
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                TempData["ErrorMessage"] = "You do not have permission to create containers.";
                return RedirectToPage("./Index");
            }

            return Page();
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        public async Task<IActionResult> OnPostAsync()
        {
            // CHECK CREATE PERMISSION (prevent direct POST attacks)
            var currentUser = _userService.GetCurrentUsername();
            var canEdit = await _userService.CanEditAsync(currentUser);

            if (!canEdit)
            {
                TempData["ErrorMessage"] = "You do not have permission to create containers.";
                return RedirectToPage("./Index");
            }

            // Normalize uppercase
            string Normalize(string? value)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length == 0) return string.Empty;
                trimmed = Regex.Replace(trimmed, "\\s+", " ");
                return trimmed.ToUpperInvariant();
            }

            // only capitalize the prefix
            string NormalizeItemNo(string? itemNo)
            {
                var trimmed = (itemNo ?? string.Empty).Trim();
                if (trimmed.Length < 3) return trimmed;
                
                // Capitalize only the prefix
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

            // Check for duplicates (case-insensitive comparison for safety)
            var exists = await _context.ReturnableContainers
                .AsNoTracking()
                .AnyAsync(rc => rc.ItemNo.ToUpper() == ReturnableContainers.ItemNo.ToUpper());

            if (exists)
            {
                ModelState.AddModelError("ReturnableContainers.ItemNo", "This Item No already exists.");
                return Page();
            }

            // START TRANSACTION - Ensure atomic operation
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Create the container
                _context.ReturnableContainers.Add(ReturnableContainers);
                await _context.SaveChangesAsync();

                // Log the CREATE action
                await _auditService.LogCreateAsync(ReturnableContainers.ItemNo, ReturnableContainers, currentUser);

                // COMMIT - Both create and audit log succeed together
                await transaction.CommitAsync();
                TempData["SuccessMessage"] = $"Container {ReturnableContainers.ItemNo} successfully created.";

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                // ROLLBACK on any error - Nothing gets saved
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "An error occurred while creating the container. Please try again.";
                return Page();
            }
        }
    }
}
