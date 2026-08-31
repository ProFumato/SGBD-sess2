import { apiRequest } from "./client";

export interface PublicMatchParticipant {
  memberId: number;
  matricule: string;
  displayName: string;
}

export interface PublicMatch {
  matchId: number;
  courtId: number;
  courtName: string;
  siteId: number;
  startsAt: string;
  endsAt: string;
  availablePlaces: number;
  participants: PublicMatchParticipant[];
}

export interface PublicMatchJoinResult {
  matchId: number;
  matchParticipantId: number;
  paymentId: number;
}

export function getPublicMatches(matricule: string): Promise<PublicMatch[]> {
  return apiRequest<PublicMatch[]>(`/api/matches/public?matricule=${encodeURIComponent(matricule)}`);
}

export function joinPublicMatch(matchId: number, matricule: string): Promise<PublicMatchJoinResult> {
  return apiRequest<PublicMatchJoinResult>(`/api/matches/${matchId}/join?matricule=${encodeURIComponent(matricule)}`, {
    method: "POST",
  });
}

export interface MatchParticipant {
  matchParticipantId: number;
  memberId: number;
  matricule: string;
  displayName: string;
  isOrganizer: boolean;
  participationStatus: string;
  isPaid: boolean;
}

export interface PrivateMatchOverview {
  matchId: number;
  courtId: number;
  courtName: string;
  siteId: number;
  siteName: string;
  startsAt: string;
  endsAt: string;
  participants: MatchParticipant[];
}

export function getPrivateMatches(matricule: string): Promise<PrivateMatchOverview[]> {
  return apiRequest<PrivateMatchOverview[]>(
    `/api/matches/private?matricule=${encodeURIComponent(matricule)}`,
  );
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
