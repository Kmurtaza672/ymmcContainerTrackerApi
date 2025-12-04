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

namespace YmmcContainerTrackerApi.Pages_ReturnableContainers
{
    public class CreateModel : PageModel
    {
        private readonly YmmcContainerTrackerApi.Data.AppDbContext _context;

        public CreateModel(YmmcContainerTrackerApi.Data.AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public ReturnableContainers ReturnableContainers { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            // Normalize key fields before validation
            ReturnableContainers.ItemNo = ReturnableContainers.ItemNo?.Trim() ?? string.Empty;
            ReturnableContainers.PackingCode = ReturnableContainers.PackingCode?.Trim() ?? string.Empty;
            ReturnableContainers.PrefixCode = ReturnableContainers.PrefixCode?.Trim() ?? string.Empty;

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

            // Check for duplicates (by ItemNo)
            var exists = await _context.ReturnableContainers
                .AsNoTracking()
                .AnyAsync(rc => rc.ItemNo == ReturnableContainers.ItemNo);

            if (exists)
            {
                ModelState.AddModelError("ReturnableContainers.ItemNo", "This Item No already exists.");
                return Page();
            }

            _context.ReturnableContainers.Add(ReturnableContainers);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
