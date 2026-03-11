export type DocumentTypeKey =
  | "pdf"
  | "word"
  | "ppt"
  | "excel"
  | "markdown"
  | "text"
  | "web"
  | "image"
  | "video"
  | "audio"
  | "file";

export function getDocumentTypeKey(contentType?: string | null): DocumentTypeKey {
  const normalized = String(contentType || "").toLowerCase();

  if (normalized === "application/pdf") return "pdf";
  if (normalized.includes("word") || normalized === "application/msword") return "word";
  if (normalized.includes("presentation") || normalized.includes("powerpoint")) return "ppt";
  if (normalized.includes("spreadsheet") || normalized.includes("excel")) return "excel";
  if (normalized.startsWith("text/markdown")) return "markdown";
  if (normalized.startsWith("text/plain")) return "text";
  if (normalized.startsWith("text/html")) return "web";
  if (normalized.startsWith("image/")) return "image";
  if (normalized.startsWith("video/")) return "video";
  if (normalized.startsWith("audio/")) return "audio";

  return "file";
}

export function getDocumentTypeLabel(contentType?: string | null): string {
  const labels: Record<DocumentTypeKey, string> = {
    pdf: "PDF",
    word: "Word",
    ppt: "PPT",
    excel: "Excel",
    markdown: "Markdown",
    text: "TXT",
    web: "网页",
    image: "图片",
    video: "视频",
    audio: "音频",
    file: "文件",
  };

  return labels[getDocumentTypeKey(contentType)];
}
