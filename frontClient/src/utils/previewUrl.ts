function isAbsoluteUrl(value: string) {
  return /^https?:\/\//i.test(value);
}

function normalizePath(value: string) {
  if (!value) {
    return "";
  }

  return value.startsWith("/") ? value : `/${value}`;
}

export function resolvePreviewRequestUrl(
  previewUrl: string,
  baseUrl = import.meta.env.VITE_API_BASE_URL || "/api",
) {
  if (!previewUrl) {
    return previewUrl;
  }

  if (isAbsoluteUrl(previewUrl)) {
    return previewUrl;
  }

  const normalizedPreviewPath = normalizePath(previewUrl);
  const normalizedBaseUrl = (baseUrl || "").replace(/\/+$/, "");

  if (!normalizedBaseUrl) {
    return normalizedPreviewPath;
  }

  if (isAbsoluteUrl(normalizedBaseUrl)) {
    return `${normalizedBaseUrl}${normalizedPreviewPath}`;
  }

  const normalizedBasePath = normalizePath(normalizedBaseUrl);
  if (
    normalizedPreviewPath === normalizedBasePath ||
    normalizedPreviewPath.startsWith(`${normalizedBasePath}/`)
  ) {
    return normalizedPreviewPath;
  }

  return `${normalizedBasePath}${normalizedPreviewPath}`;
}
