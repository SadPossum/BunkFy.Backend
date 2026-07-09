namespace Architecture.Tests.Support;

using System.Xml.Linq;

internal sealed class ProjectFile
{
    public ProjectFile(string path)
    {
        this.Path = path;
        this.RepositoryPath = RepositoryPaths.ToRepositoryPath(path);
        this.Name = System.IO.Path.GetFileNameWithoutExtension(path);
        this.Document = XDocument.Load(path);
        this.ProjectReferences = this.Document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        this.PackageReferences = this.Document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    public string Path { get; }
    public string RepositoryPath { get; }
    public string Name { get; }
    public XDocument Document { get; }
    public IReadOnlyList<string> ProjectReferences { get; }
    public IReadOnlyList<string> PackageReferences { get; }

    public static ProjectFile[] All() =>
        RepositoryPaths.EnumerateFiles("src", "*.csproj")
            .Concat(RepositoryPaths.EnumerateFiles("tests", "*.csproj"))
            .OrderBy(RepositoryPaths.ToRepositoryPath, StringComparer.Ordinal)
            .Select(path => new ProjectFile(path))
            .ToArray();
}
