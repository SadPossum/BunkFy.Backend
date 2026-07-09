namespace Architecture.Tests.Support;

internal static class RepositoryPaths
{
    public static string Root { get; } = FindRepositoryRoot();

    public static string Resolve(params string[] segments) =>
        Path.Combine([Root, .. segments]);

    public static string Read(params string[] segments) =>
        File.ReadAllText(Resolve(segments));

    public static IEnumerable<string> EnumerateFiles(string relativeRoot, string searchPattern)
    {
        string root = Resolve(relativeRoot);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            : [];
    }

    public static string ToRepositoryPath(string path) =>
        Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BunkFy.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find BunkFy backend repository root.");
    }
}
