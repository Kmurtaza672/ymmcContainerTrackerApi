using Microsoft.EntityFrameworkCore;
using YmmcContainerTrackerApi.Data;
using YmmcContainerTrackerApi.Models;

namespace YmmcContainerTrackerApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public UserService(AppDbContext context, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public bool IsAuthenticationEnabled =>
        _configuration.GetValue<bool>("Authentication:Enabled", false);

    public string GetCurrentUserIdentity()
    {
        // When Windows Auth is enabled, this gets "DOMAIN\username"
        var identity = _httpContextAccessor.HttpContext?.User?.Identity?.Name;

        return identity ?? string.Empty;
    }

    public string GetCurrentUsername()
    {
        var identity = GetCurrentUserIdentity();
        
        // Extract username from "DOMAIN\username" format
        int backslashIndex = identity.IndexOf("\\");
        if (backslashIndex >= 0)
        {
            return identity.Substring(backslashIndex + 1);
        }

        return identity; // Return as-is if no domain prefix
    }

    public async Task<bool> CanEditAsync(string username)
    {
        // If authentication is disabled, everyone can edit
        if (!IsAuthenticationEnabled)
            return true;

        var role = await GetUserRoleAsync(username);
        return role is "Admin" or "Editor";
    }

    public async Task<bool> CanViewAsync(string username)
    {
        // If authentication is disabled, everyone can view
        if (!IsAuthenticationEnabled)
            return true;

        var role = await GetUserRoleAsync(username);
        return role is "Admin" or "Editor" or "Viewer";
    }

    public async Task<string> GetUserRoleAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "None";

        try
        {
            var userRole = await _context.UserRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(ur => ur.Username == username.Trim());

            return userRole?.Role ?? "None";
        }
        catch
        {
            // If table doesn't exist yet or other DB error, return None
            return "None";
        }
    }

    public async Task<UserDisplayInfo> GetUserDisplayInfoAsync(string username)
    {
        var role = await GetUserRoleAsync(username);
        var canEdit = await CanEditAsync(username);
        var canView = await CanViewAsync(username);

        try
        {
            var userRole = await _context.UserRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(ur => ur.Username == username.Trim());

            return new UserDisplayInfo
            {
                Username = username,
                DisplayName = userRole?.DisplayName ?? username,
                Email = userRole?.Email ?? "",
                Role = role,
                CanEdit = canEdit,
                CanView = canView
            };
        }
        catch
        {
            return new UserDisplayInfo
            {
                Username = username,
                DisplayName = username,
                Email = "",
                Role = role,
                CanEdit = canEdit,
                CanView = canView
            };
        }
    }
}
