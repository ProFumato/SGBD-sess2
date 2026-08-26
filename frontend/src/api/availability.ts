import { apiRequest } from "./client";

export interface AvailableSlot {
  courtId: number;
  courtName: string;
  startAt: string;
  endAt: string;
}

export type ReservationVisibility = "Private" | "Public";

export interface ReservationResult {
  matchId: number;
  courtId: number;
  startAt: string;
  endAt: string;
  visibility: ReservationVisibility;
}

export function getAvailability(
  matricule: string,
  siteId: number,
  date: string,
  signal?: AbortSignal,
): Promise<AvailableSlot[]> {
  const params = new URLSearchParams({ matricule, siteId: String(siteId), date });
  return apiRequest<AvailableSlot[]>(`/api/availability?${params}`, { signal });
}

export function createReservation(input: {
  matricule: string;
  courtId: number;
  date: string;
  startTime: string;
  visibility: ReservationVisibility;
}): Promise<ReservationResult> {
  return apiRequest<ReservationResult>("/api/reservations", {
    method: "POST",
    body: input,
  });
}
