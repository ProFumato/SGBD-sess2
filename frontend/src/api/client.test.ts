import { describe, expect, it, vi } from "vitest";
import { apiRequest } from "./client";

describe("apiRequest", () => {
  it("adds JSON and actor headers and returns a JSON response", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      apiRequest<{ ok: boolean }>("/api/admin/example", {
        method: "POST",
        actorMatricule: "G0001",
        body: { value: 1 },
      }),
    ).resolves.toEqual({ ok: true });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/admin/example"),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ value: 1 }),
        headers: expect.any(Headers),
      }),
    );
    const headers = fetchMock.mock.calls[0][1].headers as Headers;
    expect(headers.get("X-Actor-Matricule")).toBe("G0001");
    expect(headers.get("Content-Type")).toBe("application/json");
  });

  it("turns problem details into an ApiError", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ title: "Forbidden", detail: "Not allowed." }), {
          status: 403,
          headers: { "Content-Type": "application/problem+json" },
        }),
      ),
    );

    await expect(apiRequest("/api/admin/example")).rejects.toMatchObject({
      name: "ApiError",
      status: 403,
      message: "Not allowed.",
    });
  });

  it("reports invalid successful JSON responses instead of hiding them", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response("not-json", {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );

    await expect(apiRequest("/api/example")).rejects.toMatchObject({
      name: "ApiError",
      status: 200,
      message: "Unexpected token 'o', \"not-json\" is not valid JSON",
    });
  });
});
