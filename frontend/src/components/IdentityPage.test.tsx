import { describe, expect, it } from "vitest";
import { matriculePattern } from "../validation/matricule";

describe("matriculePattern", () => {
  it.each(["G0001", "S00001", "L00001"])("accepts %s", (matricule) => {
    expect(matriculePattern.test(matricule)).toBe(true);
  });

  it.each(["g0001", "G001", "S0001", "X00001", "G00001"])("rejects %s", (matricule) => {
    expect(matriculePattern.test(matricule)).toBe(false);
  });
});
