using System.Security.Claims;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoressaDbContext _db;
    private Guid? _familyId;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IMemoressaDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public bool IsAuthenticated => UserId.HasValue;

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
    }

    public Guid? FamilyId
    {
        get
        {
            if (_familyId.HasValue)
            {
                return _familyId;
            }

            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("family_id");
            if (Guid.TryParse(claim, out var familyId))
            {
                _familyId = familyId;
                return familyId;
            }

            if (!UserId.HasValue)
            {
                return null;
            }

            _familyId = _db.FamilyMemberships.AsNoTracking()
                .Where(m => m.UserId == UserId.Value)
                .Select(m => m.FamilyId)
                .FirstOrDefault();

            return _familyId == Guid.Empty ? null : _familyId;
        }
    }
}
