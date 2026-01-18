using System;
using System.IO;

namespace xsm.Logic.Helpers
{
	public static class SoftwareLinkParser
	{
		public static bool TryExtractCodename(string? link, out string codename)
		{
			codename = string.Empty;
			if (string.IsNullOrWhiteSpace(link))
			{
				return false;
			}

			if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
			{
				return false;
			}

			var fileName = Path.GetFileName(uri.LocalPath);
			if (string.IsNullOrWhiteSpace(fileName))
			{
				return false;
			}

			var baseName = Path.GetFileNameWithoutExtension(fileName);
			if (string.IsNullOrWhiteSpace(baseName))
			{
				return false;
			}

			var separatorIndex = baseName.IndexOf('_');
			var firstToken = separatorIndex > 0 ? baseName[..separatorIndex] : baseName;
			if (string.IsNullOrWhiteSpace(firstToken))
			{
				return false;
			}

			codename = firstToken;
			return true;
		}
	}
}
