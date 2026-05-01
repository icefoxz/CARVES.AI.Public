function buildStatus(component, safeMode) {
  const normalizedComponent =
    typeof component === "string" && component.trim().length > 0
      ? component.trim()
      : "unknown";
  const mode = safeMode ? "safe" : "standard";
  return `${normalizedComponent}:${mode}`;
}

module.exports = { buildStatus };
