using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentLibraryQueryTests
{
    [Theory]
    [InlineData("我有多少張照片?", AgentLibraryIntent.CountPhotos)]
    [InlineData("我有多少张照片", AgentLibraryIntent.CountPhotos)]
    [InlineData("how many photos do I have", AgentLibraryIntent.CountPhotos)]
    [InlineData("显示所有照片", AgentLibraryIntent.ListPhotos)]
    [InlineData("顯示全部相片", AgentLibraryIntent.ListPhotos)]
    [InlineData("show all my photos", AgentLibraryIntent.ListPhotos)]
    [InlineData("happy", AgentLibraryIntent.None)]
    [InlineData("今天9月的照片", AgentLibraryIntent.None)]
    public void DetectIntent_ClassifiesLibraryQuestions(string query, AgentLibraryIntent expected)
    {
        Assert.Equal(expected, AgentLibraryQuery.DetectIntent(query));
    }
}
