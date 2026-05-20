using System.Security.Cryptography;
using System.Text;

namespace LocalRag.Infrastructure.Qdrant;

public sealed record QdrantPoint(
    string Id,
    float[] Vector,
    Dictionary<string, object> Payload)
{
    public static QdrantPoint FromChunk(string source, int chunkIndex, string text, float[] vector)
    {
        var relativeSource = Path.GetRelativePath(Environment.CurrentDirectory, source);
        return new QdrantPoint(
            Id: CreateStableGuid($"{relativeSource}:{chunkIndex}:{text}").ToString(),
            Vector: vector,
            Payload: new Dictionary<string, object>
            {
                ["source"] = relativeSource,
                ["chunk_index"] = chunkIndex,
                ["text"] = text
            });
    }

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash[..16]);
    }
}
