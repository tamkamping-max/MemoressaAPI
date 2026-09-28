using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentIntentDetectorTests
{
    [Theory]
    [InlineData("我有多少張照片?", AgentIntentKind.CountPhotos)]
    [InlineData("我有多少张照片", AgentIntentKind.CountPhotos)]
    [InlineData("how many photos do I have", AgentIntentKind.CountPhotos)]
    [InlineData("显示所有照片", AgentIntentKind.ListPhotos)]
    [InlineData("顯示全部相片", AgentIntentKind.ListPhotos)]
    [InlineData("show all my photos", AgentIntentKind.ListPhotos)]
    [InlineData("最近7天有多少张", AgentIntentKind.CountRecentPhotos, 7)]
    [InlineData("最近 7 天有几张照片", AgentIntentKind.CountRecentPhotos, 7)]
    [InlineData("最近一周的照片", AgentIntentKind.ListRecentPhotos, 7)]
    [InlineData("存储空间用了多少", AgentIntentKind.StorageUsage)]
    [InlineData("儲存空間", AgentIntentKind.StorageUsage)]
    [InlineData("上传进度", AgentIntentKind.UploadStatus)]
    [InlineData("上傳進度", AgentIntentKind.UploadStatus)]
    [InlineData("你能做什么", AgentIntentKind.Help)]
    [InlineData("happy", AgentIntentKind.None)]
    [InlineData("今天9月的照片", AgentIntentKind.None)]
    public void Detect_ClassifiesAgentIntents(string query, AgentIntentKind expectedKind, int expectedDays = 7)
    {
        var intent = AgentIntentDetector.Detect(query);
        Assert.Equal(expectedKind, intent.Kind);
        if (expectedKind is AgentIntentKind.CountRecentPhotos or AgentIntentKind.ListRecentPhotos)
        {
            Assert.Equal(expectedDays, intent.RecentDays);
        }
    }
}
