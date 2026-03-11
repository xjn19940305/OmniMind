import type { ChatReference } from "../types";

type RawChatReference = Omit<ChatReference, "hitCount" | "previewUrl">;

function normalizeReference(value: unknown): RawChatReference | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const item = value as Record<string, unknown>;
  const documentId = String(item.documentId ?? item.DocumentId ?? "");
  const documentTitle = String(item.documentTitle ?? item.DocumentTitle ?? "");
  const chunkId = String(item.chunkId ?? item.ChunkId ?? "");
  const snippet = String(item.snippet ?? item.Snippet ?? "");
  const sourceType = String(item.sourceType ?? item.SourceType ?? "");
  const rawScore = item.score ?? item.Score;

  if (!documentId || !documentTitle || !chunkId || !snippet) {
    return null;
  }

  if (sourceType !== "knowledge_base" && sourceType !== "document") {
    return null;
  }

  return {
    documentId,
    documentTitle,
    chunkId,
    snippet,
    score: typeof rawScore === "number" ? rawScore : undefined,
    sourceType,
  };
}

function buildPreviewUrl(reference: RawChatReference) {
  return reference.sourceType === "document"
    ? `/api/Chat/files/${reference.documentId}`
    : `/api/Document/${reference.documentId}/preview`;
}

export function parseChatReferences(raw?: string | null): ChatReference[] {
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }

    const grouped = new Map<string, ChatReference>();

    for (const entry of parsed) {
      const reference = normalizeReference(entry);
      if (!reference) {
        continue;
      }

      const key = `${reference.sourceType}:${reference.documentId}`;
      const existing = grouped.get(key);

      if (!existing) {
        grouped.set(key, {
          ...reference,
          hitCount: 1,
          previewUrl: buildPreviewUrl(reference),
        });
        continue;
      }

      existing.hitCount += 1;
      existing.score = Math.max(existing.score ?? 0, reference.score ?? 0);

      const snippets = new Set(
        existing.snippet
          .split("\n")
          .map((item) => item.trim())
          .filter(Boolean),
      );
      snippets.add(reference.snippet.trim());
      existing.snippet = Array.from(snippets).slice(0, 3).join("\n");
    }

    return Array.from(grouped.values());
  } catch {
    return [];
  }
}
