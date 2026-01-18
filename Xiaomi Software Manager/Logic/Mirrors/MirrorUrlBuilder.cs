using System;

namespace xsm.Logic.Mirrors;

public static class MirrorUrlBuilder
{
	public static bool TryBuildMirrorUri(Uri baseUri, string domain, out Uri mirrorUri, out string? error)
	{
		if (baseUri == null)
		{
			throw new ArgumentNullException(nameof(baseUri));
		}

		if (string.IsNullOrWhiteSpace(domain))
		{
			mirrorUri = baseUri;
			error = "Domain is empty.";
			return false;
		}

		try
		{
			var builder = new UriBuilder(baseUri)
			{
				Host = domain,
				Query = string.Empty,
				Fragment = string.Empty
			};
			mirrorUri = builder.Uri;
			error = null;
			return true;
		}
		catch (Exception ex)
		{
			mirrorUri = baseUri;
			error = ex.Message;
			return false;
		}
	}
}
