using System;
using System.IO;

namespace xsm.Data;

public static class DbPath
{
    public static string GetDefaultPath()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        return Path.Combine(dataDir, "xsm.db");
    }
}
