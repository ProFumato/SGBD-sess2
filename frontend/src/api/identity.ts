import { apiRequest } from "./client";
import type { Identity } from "../state/identity";

export function identifyMember(matricule: string, signal?: AbortSignal): Promise<Identity> {
  return apiRequest<Identity>(`/api/identity/members/${encodeURIComponent(matricule)}`, { signal });
}
