using BoneVisQA.Services.Infrastructure;
using Xunit;

namespace BoneVisQA.API.Tests.Infrastructure;

public sealed class SessionAdvisoryLockKeyTests
{
    [Fact]
    public void ToInt64_IsStableForSameSession()
    {
        var sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal(SessionAdvisoryLockKey.ToInt64(sessionId), SessionAdvisoryLockKey.ToInt64(sessionId));
    }

    [Fact]
    public void ToInt64_DiffersForDifferentSessions()
    {
        var a = SessionAdvisoryLockKey.ToInt64(Guid.NewGuid());
        var b = SessionAdvisoryLockKey.ToInt64(Guid.NewGuid());
        Assert.NotEqual(a, b);
    }
}
