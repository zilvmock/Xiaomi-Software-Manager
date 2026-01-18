using System;
using System.Threading;

namespace xsm;

internal sealed class SingleInstanceGuard : IDisposable
{
	private readonly string _name;
	private Mutex? _mutex;
	private bool _ownsMutex;

	private SingleInstanceGuard(string name)
	{
		_name = name;
	}

	public static SingleInstanceGuard? TryAcquire(string name)
	{
		var guard = new SingleInstanceGuard(name);
		return guard.TryAcquire() ? guard : null;
	}

	private bool TryAcquire()
	{
		try
		{
			_mutex = new Mutex(true, _name, out var createdNew);
			_ownsMutex = createdNew;
			if (!createdNew)
			{
				_mutex.Dispose();
				_mutex = null;
			}

			return createdNew;
		}
		catch
		{
			return false;
		}
	}

	public void Dispose()
	{
		if (_mutex == null)
		{
			return;
		}

		if (_ownsMutex)
		{
			try
			{
				_mutex.ReleaseMutex();
			}
			catch (ApplicationException)
			{
			}
		}

		_mutex.Dispose();
		_mutex = null;
	}
}
