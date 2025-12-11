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
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;

        public DetailsModel(AppDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public ReturnableContainers ReturnableContainers { get; set; } = default!;
        public bool CanEdit { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var currentUser = _userService.GetCurrentUsername();
            CanEdit = await _userService.CanEditAsync(currentUser);

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
    }
}
