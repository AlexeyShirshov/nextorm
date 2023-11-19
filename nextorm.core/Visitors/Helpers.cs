public static class Helpers
{
    public static string ToShortString(this Guid guid)
    {
        if (guid == Guid.Empty)
            return string.Empty;

        var base64Guid = Convert.ToBase64String(guid.ToByteArray());

        // Replace URL unfriendly characters with better ones
        base64Guid = base64Guid.Replace('+', '-').Replace('/', '_');

        // Remove the trailing ==
        return base64Guid[..^2];
    }
}