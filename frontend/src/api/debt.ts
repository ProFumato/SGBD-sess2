import { apiRequest } from "./client";

export interface MemberDebt {
  debtId: number;
  matchId: number;
  courtName: string;
  siteId: number;
  startsAt: string;
  initialAmount: number;
  outstandingAmount: number;
}

export function getOutstandingDebts(matricule: string): Promise<MemberDebt[]> {
  return apiRequest<MemberDebt[]>(
    `/api/debts?matricule=${encodeURIComponent(matricule)}`,
  );
}

export function getMemberDebts(actorMatricule: string, memberMatricule: string): Promise<MemberDebt[]> {
  return apiRequest<MemberDebt[]>(
    `/api/debts/admin/${encodeURIComponent(memberMatricule)}`,
    { actorMatricule },
  );
}

export function clearMemberDebts(actorMatricule: string, memberMatricule: string): Promise<void> {
  return apiRequest<void>(
    `/api/debts/admin/${encodeURIComponent(memberMatricule)}`,
    { method: "DELETE", actorMatricule },
  );
}
