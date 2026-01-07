using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YmmcContainerTrackerApi.Services;

namespace YmmcContainerTrackerApi.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILdapService _ldapService;

        public IndexModel(IUserService userService, ILdapService ldapService)
        {
            _userService = userService;
            _ldapService = ldapService;
        }

        // User Information
        public string CurrentUser { get; set; } = string.Empty;
        public string UserIdentity { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public bool HasAccess { get; set; }
        public bool HasAdAccess { get; set; }
        public string DenialReason { get; set; } = string.Empty;

        // Permissions
        public bool CanViewContainers { get; set; }
        public bool CanEditContainers { get; set; }
        public bool CanAccessReports { get; set; }
        public bool CanAccessMaintenance { get; set; }

        // Helper property to get only granted permissions
        public List<string> GrantedPermissions
        {
            get
            {
                var permissions = new List<string>();
                
                if (CanViewContainers)
                    permissions.Add("View Containers");
                
                if (CanEditContainers)
                    permissions.Add("Edit Containers");
                
                if (CanAccessMaintenance)
                    permissions.Add("Access Maintenance");
                
                if (CanAccessReports)
                    permissions.Add("View Reports");
                
                return permissions;
            }
        }

        public async Task OnGetAsync()
        {
            // Get Windows authenticated user
            UserIdentity = _userService.GetCurrentUserIdentity();
            CurrentUser = _userService.GetCurrentUsername();

            //  STEP 1: Check AD group membership (if configured)
            HasAdAccess = _ldapService.ValidateUserAccess(CurrentUser);

            if (!HasAdAccess)
            {
                DenialReason = "You are not a member of the required Active Directory security group.";
                HasAccess = false;
                return;
            }

            //  STEP 2: Check UserRoles database
            UserRole = await _userService.GetUserRoleAsync(CurrentUser);
            HasAccess = UserRole != "None";

            if (!HasAccess)
            {
                DenialReason = "You are not registered in the application's user database.";
                return;
            }

            //  User has both AD access AND database role
            CanViewContainers = await _userService.CanViewAsync(CurrentUser);
            CanEditContainers = await _userService.CanEditAsync(CurrentUser);
            CanAccessMaintenance = UserRole is "Admin" or "Editor";
            CanAccessReports = true;

        }
    }
}
