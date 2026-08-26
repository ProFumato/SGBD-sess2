const brusselsTimeZone = "Europe/Brussels";

function parseApiDate(value: string): { date: Date; timeZone: string } {
  if (!/[zZ]|[+-]\d{2}:?\d{2}$/.test(value)) {
    const parts = value.match(
      /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/,
    );
    if (parts) {
      return {
        date: new Date(
          Date.UTC(
            Number(parts[1]),
            Number(parts[2]) - 1,
            Number(parts[3]),
            Number(parts[4]),
            Number(parts[5]),
            Number(parts[6] ?? 0),
          ),
        ),
        timeZone: "UTC",
      };
    }
  }
  return { date: new Date(value), timeZone: brusselsTimeZone };
}

export function formatBrusselsDateTime(value: string): string {
  const parsed = parseApiDate(value);
  return new Intl.DateTimeFormat("en-BE", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: parsed.timeZone,
  }).format(parsed.date);
}

export function formatBrusselsTime(value: string): string {
  const parsed = parseApiDate(value);
  return new Intl.DateTimeFormat("en-BE", {
    timeStyle: "short",
    timeZone: parsed.timeZone,
  }).format(parsed.date);
}
