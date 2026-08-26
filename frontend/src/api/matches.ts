import { apiRequest } from "./client";

export interface MatchParticipant {
  matchParticipantId: number;
  memberId: number;
  matricule: string;
  displayName: string;
  isOrganizer: boolean;
  participationStatus: string;
  isPaid: boolean;
}

export function getPrivateParticipants(matchId: number, matricule: string): Promise<MatchParticipant[]> {
  return apiRequest<MatchParticipant[]>(
    `/api/matches/${matchId}/participants?matricule=${encodeURIComponent(matricule)}`,
  );
}

export function addPrivateParticipant(
  matchId: number,
  organizerMatricule: string,
  participantMatricule: string,
): Promise<void> {
  return apiRequest<void>(`/api/matches/${matchId}/participants`, {
    method: "POST",
    body: { organizerMatricule, participantMatricule },
  });
}

export function removePrivateParticipant(
  matchId: number,
  participantId: number,
  organizerMatricule: string,
): Promise<void> {
  return apiRequest<void>(
    `/api/matches/${matchId}/participants/${participantId}?matricule=${encodeURIComponent(organizerMatricule)}`,
    { method: "DELETE" },
  );
}

export function replacePrivateParticipant(
  matchId: number,
  participantId: number,
  organizerMatricule: string,
  participantMatricule: string,
): Promise<void> {
  return apiRequest<void>(`/api/matches/${matchId}/participants/${participantId}`, {
    method: "PUT",
    body: { organizerMatricule, participantMatricule },
  });
}
