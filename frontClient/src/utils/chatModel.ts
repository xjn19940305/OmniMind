export const DEFAULT_CHAT_MODEL = "deepseek-v3.2";

export function sanitizeModelOptions(models?: string[] | null): string[] {
  if (!models?.length) {
    return [];
  }

  const uniqueModels = new Set<string>();

  for (const model of models) {
    const trimmedModel = model?.trim();
    if (trimmedModel) {
      uniqueModels.add(trimmedModel);
    }
  }

  return Array.from(uniqueModels);
}

export function resolveSelectedModel(
  models?: string[] | null,
  currentModel?: string | null,
): string {
  const options = sanitizeModelOptions(models);
  const normalizedCurrentModel = currentModel?.trim();

  if (normalizedCurrentModel && options.includes(normalizedCurrentModel)) {
    return normalizedCurrentModel;
  }

  if (options.length > 0) {
    return options[0];
  }

  return DEFAULT_CHAT_MODEL;
}
