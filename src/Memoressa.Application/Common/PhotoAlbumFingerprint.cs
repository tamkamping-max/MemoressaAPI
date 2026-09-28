using System.Security.Cryptography;
using System.Text;

namespace Memoressa.Application.Common;

public static class PhotoAlbumFingerprint
{
    public static string Compute(IEnumerable<Guid> photoIds)
    {
        var canonical = photoIds
            .Distinct()
            .OrderBy(id => id)
            .Select(id => id.ToString("D"))
            .ToList();

        var payload = string.Join(',', canonical);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
