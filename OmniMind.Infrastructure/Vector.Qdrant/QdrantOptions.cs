namespace OmniMind.Vector.Qdrant
{
    public class QdrantOptions
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 6334;

        public bool Https { get; set; }
    }
}
