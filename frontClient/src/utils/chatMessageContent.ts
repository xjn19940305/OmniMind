export function normalizeChatMessageContent(content: string) {
  if (!content) {
    return "";
  }

  const normalized = content.replace(/\r\n/g, "\n").replace(/\r/g, "\n");
  const lines = normalized.split("\n");
  const result: string[] = [];
  let previousBlank = true;

  for (const line of lines) {
    const isBlank = line.trim().length === 0;

    if (isBlank) {
      if (!previousBlank) {
        result.push("");
      }

      previousBlank = true;
      continue;
    }

    result.push(line);
    previousBlank = false;
  }

  while (result.length > 0 && result[result.length - 1] === "") {
    result.pop();
  }

  return result.join("\n");
}
