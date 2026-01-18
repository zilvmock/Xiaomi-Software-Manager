namespace xsm.Models
{
	public sealed class ScrapeIssue
	{
		public ScrapeIssue(string linkText, string? linkHref, string? html, string reason)
		{
			LinkText = linkText;
			LinkHref = linkHref;
			Html = html;
			Reason = reason;
		}

		public string LinkText { get; }

		public string? LinkHref { get; }

		public string? Html { get; }

		public string Reason { get; }
	}
}
