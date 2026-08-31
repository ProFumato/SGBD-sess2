import { describe, expect, it } from "vitest";
import { formatBrusselsDateTime, formatBrusselsTime } from "./dateTime";

describe("Brussels date formatting", () => {
  it("preserves API local DateTime values as Brussels business time", () => {
    expect(formatBrusselsDateTime("2026-08-27T18:00:00")).toContain("18:00");
    expect(formatBrusselsTime("2026-08-27T19:30:00")).toBe("19:30");
  });
});
