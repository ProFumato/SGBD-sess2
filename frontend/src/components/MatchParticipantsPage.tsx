import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ApiError } from "../api/client";
import {
  addPrivateParticipant,
  getPrivateParticipants,
  removePrivateParticipant,
  replacePrivateParticipant,
  type MatchParticipant,
} from "../api/matches";
import { getPrivateMatches, type PrivateMatchOverview } from "../api/matches";
import { formatBrusselsDateTime } from "../formatting/dateTime";
import { useIdentity } from "../state/identity";
import { payParticipant, type PaymentOutcome, type PaymentResult } from "../api/payment";
import { EmptyState, ErrorState, LoadingState } from "./Feedback";

export function MatchParticipantsPage() {
  const { identity } = useIdentity();
  const [searchParams] = useSearchParams();
  const matchId = Number(searchParams.get("matchId"));
  const [matches, setMatches] = useState<PrivateMatchOverview[]>([]);
  const [participants, setParticipants] = useState<MatchParticipant[]>([]);
  const [participantMatricule, setParticipantMatricule] = useState("");
  const [replacementById, setReplacementById] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(true);
  const [mutating, setMutating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [paymentOutcome, setPaymentOutcome] = useState<PaymentOutcome>("Succeeded");
  const [paymentResult, setPaymentResult] = useState<PaymentResult | null>(null);
  const [payingMatchId, setPayingMatchId] = useState<number | null>(null);
  const [cardPaymentMessage, setCardPaymentMessage] = useState<Record<number, string>>({});

  const refresh = useCallback(async () => {
    if (!identity) return;
    setLoading(true);
    try {
      if (Number.isInteger(matchId) && matchId > 0) {
        setParticipants(await getPrivateParticipants(matchId, identity.member.matricule));
      } else {
        setMatches(await getPrivateMatches(identity.member.matricule));
      }
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

  async function handleCardPayment(matchIdToPay: number) {
    if (!identity) return;
    setPayingMatchId(matchIdToPay);
    setError(null);
    setCardPaymentMessage((current) => ({ ...current, [matchIdToPay]: "" }));
    try {
      const result = await payParticipant(matchIdToPay, identity.member.matricule, "Succeeded");
      setCardPaymentMessage((current) => ({
        ...current,
        [matchIdToPay]: `Payment #${result.paymentId} confirmed: €${result.totalAmount.toFixed(2)}.`,
      }));
      await refresh();
    } catch (caughtError) {
      setCardPaymentMessage((current) => ({
        ...current,
        [matchIdToPay]: caughtError instanceof ApiError ? caughtError.message : "The payment request failed.",
      }));
    } finally {
      setPayingMatchId(null);
    }
  }

  if (loading) return <LoadingState label="Loading match participants..." />;
  if (!Number.isInteger(matchId) || matchId <= 0) {
    return (
      <section className="content-card" aria-labelledby="member-matches-title">
        <p className="eyebrow">Member matches</p>
        <h2 id="member-matches-title">Private games you joined</h2>
        <p className="muted">See every participant and whether their €15 place has been paid.</p>
        {error && <ErrorState>{error}</ErrorState>}
        {matches.length === 0 && <EmptyState>You have not been added to an upcoming private game.</EmptyState>}
        <div className="public-match-list">
          {matches.map((match) => {
            const current = match.participants.find(
              (participant) => participant.matricule === identity?.member.matricule,
            );
            const paymentPending = current?.participationStatus === "Pending" && !current.isPaid;
            return (
              <article className="public-match-card" key={match.matchId}>
                <h3>Match #{match.matchId}</h3>
                <p>{match.siteName} · {match.courtName}</p>
                <p>{formatBrusselsDateTime(match.startsAt)} – {formatBrusselsDateTime(match.endsAt)}</p>
                <p>
                  Your place:{" "}
                  <strong>{current?.isPaid ? "Paid" : "Payment pending"}</strong>
                </p>
                <p>{match.participants.filter((participant) => participant.isPaid).length} of {match.participants.length} paid</p>
                {cardPaymentMessage[match.matchId] && (
                  <p className="card-payment-message" role="status">{cardPaymentMessage[match.matchId]}</p>
                )}
                {paymentPending && (
                  <button
                    className="button"
                    type="button"
                    disabled={payingMatchId !== null}
                    onClick={() => void handleCardPayment(match.matchId)}
                  >
                    {payingMatchId === match.matchId ? "Processing payment..." : "Pay my €15 place"}
                  </button>
                )}
                <Link className="button button-secondary" to={`/member/matches?matchId=${match.matchId}`}>
                  See participants
                </Link>
              </article>
            );
          })}
        </div>
      </section>
    );
  }

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
            Paid €{paymentResult.totalAmount.toFixed(2)} for the place; debt reduced by €{paymentResult.debtAmount.toFixed(2)}
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
