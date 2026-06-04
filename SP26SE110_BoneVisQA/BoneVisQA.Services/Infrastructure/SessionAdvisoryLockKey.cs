namespace BoneVisQA.Services.Infrastructure;

/// <summary>Maps a Visual QA session id to a PostgreSQL advisory lock key (single <c>bigint</c>).</summary>
public static class SessionAdvisoryLockKey
{
    /// <summary>Namespace salt so BoneVisQA locks do not collide with other app advisory keys.</summary>
    private const long NamespaceSalt = 0xB0A5_0001L;

    public static long ToInt64(Guid sessionId)
    {
        var bytes = sessionId.ToByteArray();
        var low = BitConverter.ToInt64(bytes, 0);
        var high = BitConverter.ToInt64(bytes, 8);
        return NamespaceSalt ^ low ^ high;
    }
}
