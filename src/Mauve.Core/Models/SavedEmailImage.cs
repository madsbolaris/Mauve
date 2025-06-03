using System.Text.Json.Serialization;

namespace Mauve.Core.Models;

public class SavedEmailImage(string path, string alt)
{
    public string Path { get; set; } = path;
    public string Alt { get; set; } = alt;
    public string? Summary { get; set; } = null;
}
