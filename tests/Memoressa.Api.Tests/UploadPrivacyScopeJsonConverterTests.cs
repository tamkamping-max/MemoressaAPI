using System.Text.Json;
using Memoressa.Application.DTOs;
using Memoressa.Application.Json;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class UploadPrivacyScopeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new UploadPrivacyScopeJsonConverter() }
    };

    [Theory]
    [InlineData("\"private\"", UploadPrivacyScope.OnlySelf)]
    [InlineData("\"specificMembers\"", UploadPrivacyScope.Custom)]
    [InlineData("\"friendsAndFamily\"", UploadPrivacyScope.FriendsAndFamily)]
    [InlineData("2", UploadPrivacyScope.Friends)]
    public void DeserializesStartUploadPrivacyScope(string privacyJson, UploadPrivacyScope expected)
    {
        var json = $$"""
            {
              "fileName": "a.jpg",
              "contentType": "image/jpeg",
              "originalFileName": "a.jpg",
              "originalContentType": "image/jpeg",
              "fileSizeBytes": 1000,
              "privacyScope": {{privacyJson}}
            }
            """;

        var dto = JsonSerializer.Deserialize<StartUploadRequestDto>(json, Options);
        Assert.NotNull(dto);
        Assert.Equal(expected, dto!.PrivacyScope);
    }
}
