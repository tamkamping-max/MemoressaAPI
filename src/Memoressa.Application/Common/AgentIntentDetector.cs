using System.Text.RegularExpressions;

namespace Memoressa.Application.Common;

public enum AgentIntentKind
{
    None,
    CountPhotos,
    ListPhotos,
    CountRecentPhotos,
    ListRecentPhotos,
    StorageUsage,
    UploadStatus,
    Help
}

public readonly record struct AgentIntent(AgentIntentKind Kind, int RecentDays = 7);

/// <summary>Structured Agent intents (library, storage, uploads, help) — not tag substring search.</summary>
public static partial class AgentIntentDetector
{
    [GeneratedRegex(@"(?<![0-9])(?<n>\d{1,3})\s*天", RegexOptions.CultureInvariant)]
    private static partial Regex RecentDaysRegex();

    public static AgentIntent Detect(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new AgentIntent(AgentIntentKind.None);
        }

        var trimmed = query.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (IsHelpIntent(trimmed, lower))
        {
            return new AgentIntent(AgentIntentKind.Help);
        }

        if (IsStorageIntent(trimmed, lower))
        {
            return new AgentIntent(AgentIntentKind.StorageUsage);
        }

        if (IsUploadIntent(trimmed, lower))
        {
            return new AgentIntent(AgentIntentKind.UploadStatus);
        }

        var recentDays = TryParseRecentDayWindow(trimmed, lower);
        if (recentDays.HasValue)
        {
            var listRecent = IsListIntent(trimmed, lower)
                || (MentionsPhotos(trimmed, lower) && !IsCountIntent(trimmed, lower));
            return listRecent
                ? new AgentIntent(AgentIntentKind.ListRecentPhotos, recentDays.Value)
                : new AgentIntent(AgentIntentKind.CountRecentPhotos, recentDays.Value);
        }

        if (!MentionsPhotos(trimmed, lower))
        {
            return new AgentIntent(AgentIntentKind.None);
        }

        if (IsCountIntent(trimmed, lower))
        {
            return new AgentIntent(AgentIntentKind.CountPhotos);
        }

        if (IsListIntent(trimmed, lower))
        {
            return new AgentIntent(AgentIntentKind.ListPhotos);
        }

        return new AgentIntent(AgentIntentKind.None);
    }

    private static bool MentionsPhotos(string trimmed, string lower) =>
        trimmed.Contains("照片", StringComparison.Ordinal)
        || trimmed.Contains("相片", StringComparison.Ordinal)
        || lower.Contains("photo")
        || lower.Contains("picture")
        || trimmed.Contains('张')
        || trimmed.Contains('張');

    private static int? TryParseRecentDayWindow(string trimmed, string lower)
    {
        if (trimmed.Contains("最近", StringComparison.Ordinal)
            || trimmed.Contains("近", StringComparison.Ordinal)
            || lower.Contains("last ")
            || lower.Contains("past ")
            || lower.Contains("recent"))
        {
            foreach (Match match in RecentDaysRegex().Matches(trimmed))
            {
                if (int.TryParse(match.Groups["n"].Value, out var days) && days is >= 1 and <= 365)
                {
                    return days;
                }
            }

            if (trimmed.Contains("一周", StringComparison.Ordinal)
                || trimmed.Contains("一週", StringComparison.Ordinal)
                || trimmed.Contains("本週", StringComparison.Ordinal)
                || trimmed.Contains("这周", StringComparison.Ordinal)
                || lower.Contains("past week")
                || lower.Contains("this week"))
            {
                return 7;
            }

            if (trimmed.Contains("最近", StringComparison.Ordinal) || lower.Contains("recent"))
            {
                return 7;
            }
        }

        return null;
    }

    private static bool IsHelpIntent(string trimmed, string lower) =>
        trimmed.Contains("你能做什么", StringComparison.Ordinal)
        || trimmed.Contains("你能做什麼", StringComparison.Ordinal)
        || trimmed.Contains("怎么用", StringComparison.Ordinal)
        || trimmed.Contains("怎麼用", StringComparison.Ordinal)
        || trimmed.Contains("可以做什麼", StringComparison.Ordinal)
        || trimmed.Contains("可以做什么", StringComparison.Ordinal)
        || lower is "help"
        || lower.Contains("what can you do");

    private static bool IsStorageIntent(string trimmed, string lower) =>
        trimmed.Contains("存储空间", StringComparison.Ordinal)
        || trimmed.Contains("儲存空間", StringComparison.Ordinal)
        || trimmed.Contains("存储", StringComparison.Ordinal)
        || trimmed.Contains("儲存", StringComparison.Ordinal)
        || trimmed.Contains("容量", StringComparison.Ordinal)
        || trimmed.Contains("空间", StringComparison.Ordinal)
        || trimmed.Contains("空間", StringComparison.Ordinal)
        || lower.Contains("storage")
        || lower.Contains("quota")
        || (trimmed.Contains("用了", StringComparison.Ordinal) && trimmed.Contains("空间", StringComparison.Ordinal))
        || (trimmed.Contains("剩", StringComparison.Ordinal) && (trimmed.Contains("空间", StringComparison.Ordinal) || trimmed.Contains("容量", StringComparison.Ordinal)));

    private static bool IsUploadIntent(string trimmed, string lower) =>
        trimmed.Contains("上传进度", StringComparison.Ordinal)
        || trimmed.Contains("上傳進度", StringComparison.Ordinal)
        || trimmed.Contains("上传中", StringComparison.Ordinal)
        || trimmed.Contains("上傳中", StringComparison.Ordinal)
        || trimmed.Contains("未完成", StringComparison.Ordinal)
        || trimmed.Contains("待上传", StringComparison.Ordinal)
        || trimmed.Contains("待上傳", StringComparison.Ordinal)
        || trimmed.Contains("传完", StringComparison.Ordinal)
        || trimmed.Contains("傳完", StringComparison.Ordinal)
        || lower.Contains("upload progress")
        || lower.Contains("incomplete upload")
        || lower.Contains("pending upload");

    private static bool IsCountIntent(string trimmed, string lower)
    {
        if (trimmed.Contains("多少", StringComparison.Ordinal)
            || trimmed.Contains("几张", StringComparison.Ordinal)
            || trimmed.Contains("幾張", StringComparison.Ordinal)
            || trimmed.Contains("几張", StringComparison.Ordinal)
            || trimmed.Contains("总数", StringComparison.Ordinal)
            || trimmed.Contains("總數", StringComparison.Ordinal)
            || trimmed.Contains("总共", StringComparison.Ordinal)
            || trimmed.Contains("總共", StringComparison.Ordinal)
            || trimmed.Contains("一共", StringComparison.Ordinal))
        {
            return true;
        }

        return lower.Contains("how many")
            || lower.Contains("photo count")
            || lower.Contains("number of photos");
    }

    private static bool IsListIntent(string trimmed, string lower)
    {
        if ((trimmed.Contains("所有", StringComparison.Ordinal)
             || trimmed.Contains("全部", StringComparison.Ordinal))
            && (trimmed.Contains("照片", StringComparison.Ordinal)
                || trimmed.Contains("相片", StringComparison.Ordinal)))
        {
            return true;
        }

        if (trimmed.Contains("显示", StringComparison.Ordinal)
            || trimmed.Contains("顯示", StringComparison.Ordinal)
            || trimmed.Contains("列出", StringComparison.Ordinal)
            || trimmed.Contains("给我看", StringComparison.Ordinal)
            || trimmed.Contains("給我看", StringComparison.Ordinal))
        {
            if (trimmed.Contains("照片", StringComparison.Ordinal)
                || trimmed.Contains("相片", StringComparison.Ordinal)
                || trimmed.Contains("所有", StringComparison.Ordinal)
                || trimmed.Contains("全部", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return lower.Contains("show all")
            || lower.Contains("all my photos")
            || lower.Contains("list photos")
            || lower.Contains("see all photos")
            || lower.Contains("show recent");
    }
}
