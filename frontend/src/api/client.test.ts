import { describe, expect, it, vi } from "vitest";
import { createReservation, getAvailability } from "./availability";
import { apiRequest } from "./client";
import {
  addPrivateParticipant,
  removePrivateParticipant,
  replacePrivateParticipant,
} from "./matches";

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

  it("serializes availability filters and reservation payloads", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("[]", { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ matchId: 4 }), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock);

    await getAvailability("G0001", 2, "2026-08-27");
    await createReservation({
      matricule: "G0001",
      courtId: 3,
      date: "2026-08-27",
      startTime: "18:00:00",
      visibility: "Private",
    });

    expect(fetchMock.mock.calls[0][0]).toContain(
      "/api/availability?matricule=G0001&siteId=2&date=2026-08-27",
    );
    expect(fetchMock.mock.calls[1][1]).toMatchObject({
      method: "POST",
      body: JSON.stringify({
        matricule: "G0001",
        courtId: 3,
        date: "2026-08-27",
        startTime: "18:00:00",
        visibility: "Private",
      }),
    });
  });

  it("uses organizer-scoped private participant contracts", async () => {
      const fetchMock = vi.fn()
        .mockResolvedValueOnce(new Response("[]", { status: 200 }))
        .mockResolvedValueOnce(new Response(null, { status: 204 }))
        .mockResolvedValueOnce(new Response(null, { status: 204 }))
        .mockResolvedValueOnce(new Response(null, { status: 204 }));
      vi.stubGlobal("fetch", fetchMock);

      await getAvailability("G0001", 2, "2026-08-27");
      await addPrivateParticipant(4, "G0001", "L00001");
      await removePrivateParticipant(4, 8, "G0001");
      await replacePrivateParticipant(4, 8, "G0001", "L00002");

      expect(fetchMock.mock.calls[1][1]).toMatchObject({
        method: "POST",
        body: JSON.stringify({ organizerMatricule: "G0001", participantMatricule: "L00001" }),
      });
      expect(fetchMock.mock.calls[2][0]).toContain("participants/8?matricule=G0001");
      expect(fetchMock.mock.calls[3][1]).toMatchObject({
        method: "PUT",
        body: JSON.stringify({ organizerMatricule: "G0001", participantMatricule: "L00002" }),
    });
  });
});
