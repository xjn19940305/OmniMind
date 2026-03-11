export type DocumentStatusTagType = "success" | "info" | "warning" | "danger";

const statusLabels: Record<number, string> = {
  0: "待处理",
  1: "已上传",
  2: "解析中",
  3: "已解析",
  4: "向量化中",
  5: "已向量化",
  6: "失败",
};

export function getDocumentStatusType(
  status?: number | null,
): DocumentStatusTagType {
  switch (status) {
    case 4:
      return "warning";
    case 5:
      return "success";
    case 6:
      return "danger";
    case 0:
    case 1:
    case 3:
    case 2:
    default:
      return status === 2 ? "warning" : "info";
  }
}

export function getDocumentStatusLabel(status?: number | null): string {
  if (typeof status !== "number") {
    return "未知";
  }

  return statusLabels[status] || "未知";
}
