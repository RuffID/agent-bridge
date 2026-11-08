namespace AgentBridge.Sub2Api.Configuration;

/// <summary>Разрешает server root или prefix с единственным завершающим /v1.</summary>
internal static class Sub2ApiEndpoints
{
    /// <summary>Выбирает ordinary либо прямой manifest без query; не принимает полный endpoint, encoded route или повторный /v1.</summary>
    internal static bool TryGetModelsUrl(string? address, out string url, bool manifest = false)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(address) || address.Any(char.IsWhiteSpace) || address.Any(char.IsControl))
        {
            return false;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            return false;
        }

        string path = uri.AbsolutePath.TrimEnd('/');
        if (path.Contains("%2f", StringComparison.OrdinalIgnoreCase) || path.Contains("%5c", StringComparison.OrdinalIgnoreCase)
            || path.Contains("//", StringComparison.Ordinal) || address.Contains('\\'))
        {
            return false;
        }

        int pathStart = address.IndexOf('/', address.IndexOf(':') + 3);
        string originalPath = string.Empty;
        if (pathStart >= 0)
        {
            originalPath = address[pathStart..].TrimEnd('/');
        }

        string[] segments = originalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            string decoded = Uri.UnescapeDataString(segment);
            if (decoded is "models" or "responses" or "backend-api" or "compact" or "." or "..")
            {
                return false;
            }

            if (decoded == "v1" && (segment != "v1" || segment != segments.Last()))
            {
                return false;
            }
        }

        if (segments.Count(segment => segment == "v1") > 1)
        {
            return false;
        }

        if (path.EndsWith("/v1", StringComparison.Ordinal))
        {
            path = path[..^3];
        }

        if (manifest)
        {
            url = uri.GetLeftPart(UriPartial.Authority) + path + "/backend-api/codex/models";
        }
        else
        {
            url = uri.GetLeftPart(UriPartial.Authority) + path + "/v1/models";
        }

        return true;
    }
}
