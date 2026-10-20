namespace NextORM.Core.Tests;

/// <summary>Entity with the two LOB column types used by the streaming terminal unit tests.</summary>
public class LobTestEntity
{
    public int Id { get; set; }

    public byte[]? Data { get; set; }

    public string? Body { get; set; }
}
