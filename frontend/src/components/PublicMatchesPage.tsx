import { useEffect, useMemo, useState } from "react";
import { ApiError } from "../api/client";
import { getPublicMatches, joinPublicMatch, type PublicMatch, type PublicMatchJoinResult } from "../api/matches";
import { getReservationSites, type ReservationSite } from "../api/availability";
import { formatBrusselsDateTime, formatBrusselsTime } from "../formatting/dateTime";
import { useIdentity } from "../state/identity";
import { EmptyState, ErrorState, LoadingState } from "./Feedback";

export function PublicMatchesPage() {
  const { identity } = useIdentity();
  const [matches, setMatches] = useState<PublicMatch[]>([]);
  const [sites, setSites] = useState<ReservationSite[]>([]);
  const [siteFilter, setSiteFilter] = useState("");
  const [dateFilter, setDateFilter] = useState("");
  const [joined, setJoined] = useState<PublicMatchJoinResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [joiningId, setJoiningId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function loadMatches() {
    if (!identity) return;
    setLoading(true);
    try {
      const [loadedMatches, loadedSites] = await Promise.all([
        getPublicMatches(identity.member.matricule),
        getReservationSites(),
      ]);
      setMatches(loadedMatches);
      setSites(loadedSites);
      setError(null);
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "Public matches could not be loaded.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadMatches();
  }, [identity]);

  const filteredMatches = useMemo(
    () =>
      matches.filter(
        (match) =>
          (!siteFilter || match.siteId === Number(siteFilter)) &&
          (!dateFilter || match.startsAt.slice(0, 10) === dateFilter),
      ),
    [dateFilter, matches, siteFilter],
  );

  async function handleJoin(match: PublicMatch) {
    if (!identity || joiningId !== null) return;
    if (!window.confirm("Join this public match? Your €15 place is paid atomically and places are first-paid, first-served.")) {
      return;
    }
    setJoiningId(match.matchId);
    setError(null);
    try {
      setJoined(await joinPublicMatch(match.matchId, identity.member.matricule));
      await loadMatches();
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 409) {
        setError("This public place is no longer available. The match list was refreshed.");
        await loadMatches();
      } else if (caughtError instanceof ApiError && caughtError.status === 404) {
        setError("This public match no longer exists.");
        await loadMatches();
      } else {
        setError(caughtError instanceof ApiError ? caughtError.message : "The join request failed.");
      }
    } finally {
      setJoiningId(null);
    }
  }

  if (loading) return <LoadingState label="Loading public matches..." />;
  if (joined) {
    return (
      <section className="content-card" aria-labelledby="join-success-title">
        <p className="eyebrow">Join confirmed</p>
        <h2 id="join-success-title">You joined match #{joined.matchId}.</h2>
        <p>Your €15 place payment was accepted. Payment #{joined.paymentId} confirms your place.</p>
      </section>
    );
  }

  return (
    <section className="content-card" aria-labelledby="public-matches-title">
      <p className="eyebrow">Public matches</p>
      <h2 id="public-matches-title">Find a game across sites</h2>
      <p className="muted">Joining pays and confirms one €15 place atomically. The first successful payment gets the place.</p>
      <div className="filter-row">
        <label htmlFor="public-site-filter">Site</label>
        <select id="public-site-filter" value={siteFilter} onChange={(event) => setSiteFilter(event.target.value)}>
          <option value="">All sites</option>
          {sites.map((site) => <option key={site.siteId} value={site.siteId}>{site.name}</option>)}
        </select>
        <label htmlFor="public-date-filter">Date</label>
        <input id="public-date-filter" type="date" value={dateFilter} onChange={(event) => setDateFilter(event.target.value)} />
      </div>
      {error && <ErrorState>{error}</ErrorState>}
      {filteredMatches.length === 0 && <EmptyState>No public matches match the selected filters.</EmptyState>}
      <div className="public-match-list">
        {filteredMatches.map((match) => (
          <article className="public-match-card" key={match.matchId}>
            <div>
              <h3>Match #{match.matchId}</h3>
              <p>
                {sites.find((site) => site.siteId === match.siteId)?.name ?? `Site ${match.siteId}`} · {match.courtName} · {formatBrusselsDateTime(match.startsAt)}–{formatBrusselsTime(match.endsAt)}
              </p>
              <span className="muted">{match.availablePlaces} open place{match.availablePlaces === 1 ? "" : "s"} · Organizer details unavailable from API</span>
            </div>
            <button className="button" type="button" disabled={joiningId !== null || match.availablePlaces <= 0} onClick={() => void handleJoin(match)}>
              {joiningId === match.matchId ? "Joining..." : "Join for €15"}
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}
