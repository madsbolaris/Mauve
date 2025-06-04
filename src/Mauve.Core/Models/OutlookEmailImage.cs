using System.Text.Json.Serialization;

namespace Mauve.Core.Models;

public class OutlookEmailImage(string path, string alt)
{
    public string Path { get; set; } = path;
    public string Alt { get; set; } = alt;
}
