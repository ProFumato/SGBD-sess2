import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ApiError } from "../api/client";
import {
  addPrivateParticipant,
  getPrivateParticipants,
  removePrivateParticipant,
  replacePrivateParticipant,
  type MatchParticipant,
} from "../api/matches";
import { useIdentity } from "../state/identity";
import { payParticipant, type PaymentOutcome, type PaymentResult } from "../api/payment";
import { EmptyState, ErrorState, LoadingState } from "./Feedback";

export function MatchParticipantsPage() {
  const { identity } = useIdentity();
  const [searchParams] = useSearchParams();
  const matchId = Number(searchParams.get("matchId"));
  const [participants, setParticipants] = useState<MatchParticipant[]>([]);
  const [participantMatricule, setParticipantMatricule] = useState("");
  const [replacementById, setReplacementById] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(true);
  const [mutating, setMutating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [paymentOutcome, setPaymentOutcome] = useState<PaymentOutcome>("Succeeded");
  const [paymentResult, setPaymentResult] = useState<PaymentResult | null>(null);

  const refresh = useCallback(async () => {
    if (!identity || !Number.isInteger(matchId) || matchId <= 0) return;
    setLoading(true);
    try {
      setParticipants(await getPrivateParticipants(matchId, identity.member.matricule));
      setError(null);
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "Participants could not be loaded.");
    } finally {
      setLoading(false);
    }
  }, [identity, matchId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const isOrganizer = participants.some(
    (participant) => participant.isOrganizer && participant.matricule === identity?.member.matricule,
  );
  const organizerHasPendingPayment = participants.some(
    (participant) =>
      participant.isOrganizer &&
      participant.matricule === identity?.member.matricule &&
      !participant.isPaid &&
      participant.participationStatus === "Pending",
  );

  async function mutate(action: () => Promise<void>) {
    setMutating(true);
    setError(null);
    try {
      await action();
      await refresh();
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "The participant change failed.");
    } finally {
      setMutating(false);
    }
  }

  async function handlePayment() {
    if (!identity || !isOrganizer) return;
    setMutating(true);
    setError(null);
    try {
      const result = await payParticipant(matchId, identity.member.matricule, paymentOutcome);
      setPaymentResult(result);
      await refresh();
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "The payment request failed.");
    } finally {
      setMutating(false);
    }
  }

  if (!Number.isInteger(matchId) || matchId <= 0) {
    return <ErrorState>A valid match ID is required to manage participants.</ErrorState>;
  }
  if (loading) return <LoadingState label="Loading match participants..." />;

  return (
    <section className="content-card" aria-labelledby="participants-title">
      <p className="eyebrow">Private match</p>
      <h2 id="participants-title">Match #{matchId} participants</h2>
      <p className="muted">Only the organizer can change pending non-organizer places.</p>
      {error && <ErrorState>{error}</ErrorState>}
      {paymentResult && (
        <div className={`payment-result ${paymentResult.outcome === "Failed" ? "payment-result-failed" : ""}`}>
          <strong>Payment #{paymentResult.paymentId}: {paymentResult.outcome}</strong>
          <span>
            Place €{paymentResult.participantAmount.toFixed(2)} + debt €{paymentResult.debtAmount.toFixed(2)} = €{paymentResult.totalAmount.toFixed(2)}
          </span>
          {paymentResult.outcome === "Failed" && <span>The place remains pending; you can retry after reviewing the result.</span>}
        </div>
      )}
      {participants.length === 0 && <EmptyState>No participants were returned.</EmptyState>}
      <div className="participant-list">
        {participants.map((participant) => {
          const canChange = isOrganizer && !participant.isOrganizer && participant.participationStatus === "Pending";
          return (
            <article className="participant-row" key={participant.matchParticipantId}>
              <div>
                <strong>{participant.displayName}</strong>
                <span>
                  {participant.matricule} · {participant.isOrganizer ? "Organizer" : participant.participationStatus} ·{" "}
                  {participant.isPaid ? "Paid" : "Payment pending"}
                </span>
              </div>
              {canChange && (
                <div className="participant-actions">
                  <input
                    aria-label={`Replacement matricule for ${participant.displayName}`}
                    value={replacementById[participant.matchParticipantId] ?? ""}
                    onChange={(event) =>
                      setReplacementById((current) => ({
                        ...current,
                        [participant.matchParticipantId]: event.target.value,
                      }))
                    }
                    placeholder="Replacement matricule"
                    disabled={mutating}
                  />
                  <button
                    className="button button-secondary"
                    type="button"
                    disabled={mutating || !replacementById[participant.matchParticipantId]?.trim()}
                    onClick={() =>
                      void mutate(() =>
                        replacePrivateParticipant(
                          matchId,
                          participant.matchParticipantId,
                          identity!.member.matricule,
                          replacementById[participant.matchParticipantId].trim().toUpperCase(),
                        ),
                      )
                    }
                  >
                    Replace
                  </button>
                  <button
                    className="button button-danger"
                    type="button"
                    disabled={mutating}
                    onClick={() => {
                      if (window.confirm(`Remove ${participant.displayName} from this match?`)) {
                        void mutate(() =>
                          removePrivateParticipant(matchId, participant.matchParticipantId, identity!.member.matricule),
                        );
                      }
                    }}
                  >
                    Remove
                  </button>
                </div>
              )}
            </article>
          );
        })}
      </div>
      {isOrganizer && organizerHasPendingPayment && (
        <div className="payment-panel">
          <label htmlFor="payment-outcome">Payment simulation outcome</label>
          <select
            id="payment-outcome"
            value={paymentOutcome}
            onChange={(event) => setPaymentOutcome(event.target.value as PaymentOutcome)}
            disabled={mutating}
          >
            <option value="Succeeded">Succeeded</option>
            <option value="Failed">Failed (demo)</option>
          </select>
          <button className="button" type="button" onClick={() => void handlePayment()} disabled={mutating}>
            {mutating ? "Processing payment..." : "Pay pending place"}
          </button>
        </div>
      )}
      {isOrganizer && <form
        onSubmit={(event) => {
          event.preventDefault();
          const value = participantMatricule.trim().toUpperCase();
          if (value) {
            void mutate(() => addPrivateParticipant(matchId, identity!.member.matricule, value));
            setParticipantMatricule("");
          }
        }}
      >
        <label htmlFor="participant-matricule">Add participant by matricule</label>
        <input
          id="participant-matricule"
          value={participantMatricule}
          onChange={(event) => setParticipantMatricule(event.target.value)}
          disabled={mutating}
        />
        <button className="button" type="submit" disabled={mutating || !participantMatricule.trim()}>
          Add participant
        </button>
      </form>}
    </section>
  );
}
