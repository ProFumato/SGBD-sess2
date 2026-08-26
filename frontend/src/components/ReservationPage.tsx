import { useEffect, useMemo, useState } from "react";
import { ApiError } from "../api/client";
import {
  createReservation,
  getAvailability,
  type AvailableSlot,
  type ReservationResult,
  type ReservationVisibility,
} from "../api/availability";
import { formatBrusselsDateTime, formatBrusselsTime } from "../formatting/dateTime";
import { useIdentity } from "../state/identity";
import { ErrorState, EmptyState, LoadingState } from "./Feedback";

function todayInBrussels(): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Brussels",
  }).format(new Date());
}

export function ReservationPage() {
  const { identity } = useIdentity();
  const [siteId, setSiteId] = useState("");
  const [date, setDate] = useState(todayInBrussels);
  const [slots, setSlots] = useState<AvailableSlot[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<AvailableSlot | null>(null);
  const [visibility, setVisibility] = useState<ReservationVisibility>("Private");
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reservation, setReservation] = useState<ReservationResult | null>(null);

  const parsedSiteId = Number(siteId);
  const siteRestriction =
    identity?.member.membershipCategory === "Site" &&
    identity.member.homeSiteId !== null &&
    parsedSiteId > 0 &&
    parsedSiteId !== identity.member.homeSiteId;
  const groupedSlots = useMemo(
    () =>
      slots.reduce<Map<string, AvailableSlot[]>>((groups, slot) => {
        const key = `${slot.courtId}:${slot.courtName}`;
        const courtSlots = groups.get(key) ?? [];
        courtSlots.push(slot);
        groups.set(key, courtSlots);
        return groups;
      }, new Map()),
    [slots],
  );

  useEffect(() => {
    if (!identity || !Number.isInteger(parsedSiteId) || parsedSiteId <= 0 || !date || siteRestriction) {
      setSlots([]);
      setSelectedSlot(null);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);
    setSelectedSlot(null);
    getAvailability(identity.member.matricule, parsedSiteId, date, controller.signal)
      .then(setSlots)
      .catch((caughtError: unknown) => {
        if (caughtError instanceof DOMException && caughtError.name === "AbortError") return;
        setError(caughtError instanceof ApiError ? caughtError.message : "Availability could not be loaded.");
        setSlots([]);
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [date, identity, parsedSiteId, siteRestriction]);

  async function handleSubmit() {
    if (!identity || !selectedSlot || siteRestriction) return;
    setSubmitting(true);
    setError(null);
    try {
      setReservation(
        await createReservation({
          matricule: identity.member.matricule,
          courtId: selectedSlot.courtId,
          date,
          startTime: selectedSlot.startAt.slice(11, 19),
          visibility,
        }),
      );
    } catch (caughtError) {
      setError(
        caughtError instanceof ApiError
          ? caughtError.message
          : "The reservation could not be created. Check the API connection and try again.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (reservation) {
    return (
      <section className="content-card" aria-labelledby="reservation-confirmed-title">
        <p className="eyebrow">Reservation confirmed</p>
        <h2 id="reservation-confirmed-title">Match #{reservation.matchId} is ready.</h2>
        <p>
          {formatBrusselsDateTime(reservation.startAt)}–{formatBrusselsTime(reservation.endAt)} ·{" "}
          {reservation.visibility} match · court {reservation.courtId}
        </p>
        <p className="muted">The organizer payment state will be shown by the payment workflow.</p>
      </section>
    );
  }

  return (
    <section className="content-card" aria-labelledby="reservation-title">
      <p className="eyebrow">Availability and reservation</p>
      <h2 id="reservation-title">Choose a Brussels-local slot</h2>
      <p className="muted">
        Availability is read from the server. Matches last 90 minutes and require a 15-minute gap.
      </p>
      <form onSubmit={(event) => { event.preventDefault(); void handleSubmit(); }}>
        <label htmlFor="site-id">Site ID</label>
        <input id="site-id" type="number" min="1" value={siteId} onChange={(event) => setSiteId(event.target.value)} />
        <label htmlFor="reservation-date">Date</label>
        <input id="reservation-date" type="date" value={date} onChange={(event) => setDate(event.target.value)} />
        {siteRestriction && <ErrorState>Site members can reserve only at their home site.</ErrorState>}
        <fieldset>
          <legend>Match visibility</legend>
          <label>
            <input type="radio" name="visibility" value="Private" checked={visibility === "Private"} onChange={() => setVisibility("Private")} />
            Private
          </label>
          <label>
            <input type="radio" name="visibility" value="Public" checked={visibility === "Public"} onChange={() => setVisibility("Public")} />
            Public
          </label>
        </fieldset>
      </form>
      {loading && <LoadingState label="Loading available courts..." />}
      {error && <ErrorState>{error}</ErrorState>}
      {!loading && !error && Number.isInteger(parsedSiteId) && parsedSiteId > 0 && !siteRestriction && slots.length === 0 && (
        <EmptyState>No available slots were returned for this site and date.</EmptyState>
      )}
      {!loading && groupedSlots.size > 0 && (
        <div className="slot-groups">
          {[...groupedSlots].map(([court, courtSlots]) => (
            <div className="slot-group" key={court}>
              <h3>{courtSlots[0].courtName}</h3>
              <div className="slot-grid">
                {courtSlots.map((slot) => (
                  <button
                    className={`slot-button ${selectedSlot === slot ? "slot-button-selected" : ""}`}
                    type="button"
                    key={`${slot.courtId}-${slot.startAt}`}
                    onClick={() => setSelectedSlot(slot)}
                  >
                    {formatBrusselsTime(slot.startAt)}–{formatBrusselsTime(slot.endAt)}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
      {selectedSlot && (
        <button className="button" type="button" onClick={() => void handleSubmit()} disabled={submitting}>
          {submitting ? "Creating reservation..." : "Confirm reservation"}
        </button>
      )}
    </section>
  );
}
