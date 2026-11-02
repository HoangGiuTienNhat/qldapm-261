/** Copy the complete source URL; if clipboard is unavailable, select that URL for manual copy. */
export async function copyTableUrl(
  url: string,
  clipboard: { writeText(value: string): Promise<void> } | undefined,
  selectUrl: () => void,
): Promise<"copied" | "selected"> {
  try {
    if (!clipboard) throw new Error("Clipboard unavailable");
    await clipboard.writeText(url);
    return "copied";
  } catch {
    selectUrl();
    return "selected";
  }
}
