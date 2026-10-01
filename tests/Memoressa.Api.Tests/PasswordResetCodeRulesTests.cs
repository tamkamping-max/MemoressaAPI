using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class PasswordResetCodeRulesTests
{
    [Fact]
    public void GenerateNumericCode_ReturnsEightDigits()
    {
        var code = PasswordResetCodeRules.GenerateNumericCode();
        Assert.Equal(8, code.Length);
        Assert.True(PasswordResetCodeRules.IsValidCodeFormat(code));
    }

    [Fact]
    public void IsValidCodeFormat_RejectsNonNumericOrWrongLength()
    {
        Assert.False(PasswordResetCodeRules.IsValidCodeFormat("1234567"));
        Assert.False(PasswordResetCodeRules.IsValidCodeFormat("123456789"));
        Assert.False(PasswordResetCodeRules.IsValidCodeFormat("12ab5678"));
    }
}
