using System.Text;
using BunkFy.DataGovernance;

if (args.Length is not 2)
{
    Console.Error.WriteLine("Usage: BunkFy.DataGovernance.Tool <catalog.json> <inventory.md>");
    return 2;
}

string catalogPath = Path.GetFullPath(args[0]);
string inventoryPath = Path.GetFullPath(args[1]);
PersonalDataCatalogDocument catalogue = PersonalDataCatalogJson.Parse(File.ReadAllBytes(catalogPath));
string inventory = PersonalDataInventoryRenderer.RenderMarkdown(catalogue);
Directory.CreateDirectory(Path.GetDirectoryName(inventoryPath)!);
File.WriteAllText(inventoryPath, inventory, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"Generated {inventoryPath} from {catalogPath}.");
return 0;
