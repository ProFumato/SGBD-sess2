import { apiRequest } from "./client";

export type PaymentOutcome = "Succeeded" | "Failed";

export interface PaymentResult {
  paymentId: number;
  matchId: number;
  matchParticipantId: number;
  participantAmount: number;
  debtAmount: number;
  totalAmount: number;
  outcome: PaymentOutcome;
}

export function payParticipant(
  matchId: number,
  matricule: string,
  outcome: PaymentOutcome = "Succeeded",
): Promise<PaymentResult> {
  return apiRequest<PaymentResult>(
    `/api/matches/${matchId}/payment?matricule=${encodeURIComponent(matricule)}&outcome=${outcome}`,
    { method: "POST" },
  );
}
