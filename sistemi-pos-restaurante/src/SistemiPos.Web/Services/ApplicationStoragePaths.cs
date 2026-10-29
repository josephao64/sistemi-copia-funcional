namespace SistemiPos.Web.Services;

public sealed class ApplicationStoragePaths
{
    public ApplicationStoragePaths(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        DataProtectionKeysPath = Path.Combine(RootPath, "DataProtection-Keys");
        UploadsPath = Path.Combine(RootPath, "Uploads");
    }

    public string RootPath { get; }
    public string DataProtectionKeysPath { get; }
    public string UploadsPath { get; }
}
