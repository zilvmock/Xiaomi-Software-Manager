namespace xsm.Logic.Downloads
{
	public enum DownloadStatus
	{
		Queued,
		Downloading,
		CheckingMd5,
		Completed,
		Warning,
		Failed,
		Canceled
	}
}
