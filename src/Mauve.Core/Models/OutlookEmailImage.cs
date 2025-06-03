using System.Text.Json.Serialization;

namespace Mauve.Core.Models;

public class OutlookEmailImage(string cid, string alt, Uri uri, byte[] bytes, string fileExtension)
{
    public string Cid { get; set; } = cid;
    public string Alt { get; set; } = alt;
    public Uri Uri { get; set; } = uri;
    [JsonIgnore] public byte[] Bytes { get; set; } = bytes;
    public string FileExtension { get; set; } = fileExtension;
}
