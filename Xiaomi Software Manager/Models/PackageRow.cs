namespace xsm.Models;

public sealed class PackageRow
{
    public PackageRow(string name, string version, string status)
    {
        Name = name;
        Version = version;
        Status = status;
    }

    public string Name { get; }

    public string Version { get; }

    public string Status { get; }
}
