namespace OmniMind.Abstractions.Storage
{
    public static class VectorCollectionName
    {
        public static string BuildKnowledgeBaseCollectionName(string knowledgeBaseId)
        {
            return $"kb_{knowledgeBaseId}";
        }

        public static string BuildSessionCollectionName(string sessionId)
        {
            return $"session_{sessionId}";
        }
    }
}
