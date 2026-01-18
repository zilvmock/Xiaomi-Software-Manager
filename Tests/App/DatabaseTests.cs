using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using xsm.Data;

namespace xsm.tests.app;

public sealed class DatabaseTests
{
	[Test]
	public async Task Database_CreatesSchemaAndConnects()
	{
		await using var context = AppDbContextFactory.Create();
		await DatabaseBootstrapper.EnsureSchemaAsync(context);

		Assert.That(await context.Database.CanConnectAsync(), Is.True);
		Assert.That(File.Exists(DbPath.GetDefaultPath()), Is.True);
	}
}
