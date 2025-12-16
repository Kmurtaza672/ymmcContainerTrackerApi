using System.DirectoryServices.AccountManagement;

namespace YmmcContainerTrackerApi.Services;

public interface ILdapService
{

    /// Checks if user is a member of a specific AD group
    bool IsUserInGroup(string username, string groupName);
    

    /// Gets user information from Active Directory

    ActiveDirectoryUserInfo? GetUserInfo(string username);
    

    /// Validates if user has access to the application via AD group
    bool ValidateUserAccess(string username);
}

public class LdapService : ILdapService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LdapService> _logger;

    public LdapService(IConfiguration configuration, ILogger<LdapService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }


    /// Checks if user is a member of specific AD group
    public bool IsUserInGroup(string username, string groupName)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(groupName))
        {
            _logger.LogWarning("Invalid username or group name provided");
            return false;
        }

        try
        {
            _logger.LogInformation(" Checking if user '{Username}' is in AD group '{GroupName}'", username, groupName);

            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);
            
            if (user == null)
            {
                _logger.LogWarning(" User '{Username}' not found in Active Directory", username);
                return false;
            }

            using var group = GroupPrincipal.FindByIdentity(context, groupName);
            
            if (group == null)
            {
                _logger.LogWarning(" Group '{GroupName}' not found in Active Directory", groupName);
                return false;
            }

            bool isMember = user.IsMemberOf(group);
            
            if (isMember)
            {
                _logger.LogInformation(" User '{Username}' IS a member of '{GroupName}'", username, groupName);
            }
            else
            {
                _logger.LogWarning(" User '{Username}' is NOT a member of '{GroupName}'", username, groupName);
            }

            return isMember;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Error checking AD group membership for user '{Username}' in group '{GroupName}'", username, groupName);
            return false;
        }
    }


    /// Gets user information from Active Directory
    public ActiveDirectoryUserInfo? GetUserInfo(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Invalid username provided");
            return null;
        }

        try
        {
            _logger.LogInformation(" Retrieving AD info for user '{Username}'", username);

            using var context = new PrincipalContext(ContextType.Domain);
            using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);
            
            if (user == null)
            {
                _logger.LogWarning(" User '{Username}' not found in Active Directory", username);
                return null;
            }

            var userInfo = new ActiveDirectoryUserInfo
            {
                Username = user.SamAccountName ?? username,
                DisplayName = user.DisplayName ?? username,
                Email = user.EmailAddress ?? string.Empty,
                FirstName = user.GivenName ?? string.Empty,
                LastName = user.Surname ?? string.Empty,
                Description = user.Description ?? string.Empty,
                IsEnabled = user.Enabled ?? false
            };

            _logger.LogInformation(" AD info retrieved for '{Username}': {DisplayName}", username, userInfo.DisplayName);

            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Error retrieving AD user info for '{Username}'", username);
            return null;
        }
    }

 
    /// Validates if user has access via AD group membership
    public bool ValidateUserAccess(string username)
    {
        // Get required AD group from configuration
        var requiredGroup = _configuration.GetValue<string>("Authentication:RequiredAdGroup");

        // If no group is configured, allow all authenticated users
        if (string.IsNullOrEmpty(requiredGroup))
        {
            _logger.LogInformation(" No AD group requirement configured - allowing user '{Username}'", username);
            return true;
        }

        // Check if user is in the required group
        return IsUserInGroup(username, requiredGroup);
    }
}


/// User information from Active Directory
public class ActiveDirectoryUserInfo
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}