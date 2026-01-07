using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YmmcContainerTrackerApi.Data;
using YmmcContainerTrackerApi.Models;

namespace YmmcContainerTrackerApi.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext context,
        IUserService userService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogCreateAsync(string itemNo, ReturnableContainers newContainer, string username, string? ipAddress = null)
    {
        try
        {
            var userRole = await _userService.GetUserRoleAsync(username);
            var log = new ContainerAuditLog
            {
                ItemNo = itemNo,
                Action = "CREATE",
                Username = username,
                UserRole = userRole,
                Timestamp = DateTime.UtcNow,
                NewValues = SerializeContainer(newContainer),
                IpAddress = ipAddress ?? GetClientIpAddress(),
                UserAgent = GetUserAgent()
            };

            _context.ContainerAuditLogs.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: {Username} CREATED container {ItemNo}", username, itemNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Failed to log CREATE action for {ItemNo}", itemNo);
        }
    }

    public async Task LogUpdateAsync(string itemNo, ReturnableContainers oldContainer, ReturnableContainers newContainer, string username, string? ipAddress = null)
    {
        try
        {
            var userRole = await _userService.GetUserRoleAsync(username);
            var changedFields = GetChangedFields(oldContainer, newContainer);

            var log = new ContainerAuditLog
            {
                ItemNo = itemNo,
                Action = "UPDATE",
                Username = username,
                UserRole = userRole,
                Timestamp = DateTime.UtcNow,
                OldValues = SerializeContainer(oldContainer),
                NewValues = SerializeContainer(newContainer),
                ChangedFields = string.Join(", ", changedFields),
                IpAddress = ipAddress ?? GetClientIpAddress(),
                UserAgent = GetUserAgent()
            };

            _context.ContainerAuditLogs.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: {Username} UPDATED container {ItemNo} - Changed: {Fields}",
                username, itemNo, log.ChangedFields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Failed to log UPDATE action for {ItemNo}", itemNo);
        }
    }

    public async Task LogDeleteAsync(string itemNo, ReturnableContainers deletedContainer, string username, string? ipAddress = null)
    {
        try
        {
            var userRole = await _userService.GetUserRoleAsync(username);
            var log = new ContainerAuditLog
            {
                ItemNo = itemNo,
                Action = "DELETE",
                Username = username,
                UserRole = userRole,
                Timestamp = DateTime.UtcNow,
                OldValues = SerializeContainer(deletedContainer),
                IpAddress = ipAddress ?? GetClientIpAddress(),
                UserAgent = GetUserAgent()
            };

            _context.ContainerAuditLogs.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: {Username} DELETED container {ItemNo}", username, itemNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Failed to log DELETE action for {ItemNo}", itemNo);
        }
    }

    public async Task LogViewAsync(string itemNo, string username, string? ipAddress = null)
    {
        try
        {
            var userRole = await _userService.GetUserRoleAsync(username);
            var log = new ContainerAuditLog
            {
                ItemNo = itemNo,
                Action = "VIEW",
                Username = username,
                UserRole = userRole,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress ?? GetClientIpAddress(),
                UserAgent = GetUserAgent()
            };

            _context.ContainerAuditLogs.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogDebug("AUDIT: {Username} VIEWED container {ItemNo}", username, itemNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Failed to log VIEW action for {ItemNo}", itemNo);
        }
    }

    public async Task<List<ContainerAuditLog>> GetContainerHistoryAsync(string itemNo)
    {
        // Support case-insensitive and partial matching
        var upperItemNo = itemNo.Trim().ToUpper();
        return await _context.ContainerAuditLogs
            .Where(log => log.ItemNo.ToUpper().Contains(upperItemNo))
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync();
    }

    public async Task<List<ContainerAuditLog>> GetAuditLogsAsync(
        string? username = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageSize = 100)
    {
        var query = _context.ContainerAuditLogs.AsQueryable();

        // Support case-insensitive and partial matching for username
        if (!string.IsNullOrWhiteSpace(username))
        {
            var upperUsername = username.Trim().ToUpper();
            query = query.Where(log => log.Username.ToUpper().Contains(upperUsername));
        }

        // Exact match for action (dropdown selection)
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(log => log.Action == action);

        // Date range filters
        if (startDate.HasValue)
            query = query.Where(log => log.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(log => log.Timestamp <= endDate.Value);

        return await query
            .OrderByDescending(log => log.Timestamp)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<ContainerAuditLog>> GetRecentActivityAsync(int count = 50)
    {
        return await _context.ContainerAuditLogs
            .OrderByDescending(log => log.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    // Helper methods
    private string SerializeContainer(ReturnableContainers container)
    {
        return JsonSerializer.Serialize(new
        {
            container.ItemNo,
            container.PackingCode,
            container.PrefixCode,
            container.ContainerNumber,
            container.OutsideLength,
            container.OutsideWidth,
            container.OutsideHeight,
            container.CollapsedHeight,
            container.Weight,
            container.PackQuantity,
            container.AlternateId
        });
    }

    private List<string> GetChangedFields(ReturnableContainers old, ReturnableContainers new_)
    {
        var changes = new List<string>();

        if (old.PackingCode != new_.PackingCode) changes.Add("PackingCode");
        if (old.PrefixCode != new_.PrefixCode) changes.Add("PrefixCode");
        if (old.ContainerNumber != new_.ContainerNumber) changes.Add("ContainerNumber");
        if (old.OutsideLength != new_.OutsideLength) changes.Add("OutsideLength");
        if (old.OutsideWidth != new_.OutsideWidth) changes.Add("OutsideWidth");
        if (old.OutsideHeight != new_.OutsideHeight) changes.Add("OutsideHeight");
        if (old.CollapsedHeight != new_.CollapsedHeight) changes.Add("CollapsedHeight");
        if (old.Weight != new_.Weight) changes.Add("Weight");
        if (old.PackQuantity != new_.PackQuantity) changes.Add("PackQuantity");
        if (old.AlternateId != new_.AlternateId) changes.Add("AlternateId");

        return changes;
    }

    private string? GetClientIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
    }
}