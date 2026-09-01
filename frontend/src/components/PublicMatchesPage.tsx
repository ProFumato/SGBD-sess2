/**
 * Public Matches Page
 * Browse and join public matches organized by site and date.
 */

import { useEffect, useMemo, useState } from "react";
import { ApiError } from "../api/client";
import { getPublicMatches, joinPublicMatch, type PublicMatch } from "../api/matches";
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
  const [joinedMatchId, setJoinedMatchId] = useState<number | null>(null);
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
      const joinResult = await joinPublicMatch(match.matchId, identity.member.matricule);
      setJoinedMatchId(joinResult.matchId);
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

  return (
    <section className="content-card" aria-labelledby="public-matches-title">
      <p className="eyebrow">Public matches</p>
      <h2 id="public-matches-title">Find a game across sites</h2>
      <p className="muted">Joining pays and confirms one €15 place atomically. The first successful payment gets the place.</p>
      {joinedMatchId && (
        <div className="feedback" role="status">
          You joined match #{joinedMatchId}. Your €15 place is confirmed.
        </div>
      )}
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
        {filteredMatches.map((match) => {
          const isCurrentMemberJoined = match.participants.some((participant) => participant.matricule === identity?.member.matricule);
          const buttonLabel = joiningId === match.matchId
            ? "Joining..."
            : isCurrentMemberJoined
              ? "Joined"
              : match.availablePlaces <= 0
                ? "Full"
                : "Join for €15";

          return (
            <article className="public-match-card" key={match.matchId}>
              <div>
                <h3>Match #{match.matchId}</h3>
                <p>
                  {sites.find((site) => site.siteId === match.siteId)?.name ?? `Site ${match.siteId}`} · {match.courtName} · {formatBrusselsDateTime(match.startsAt)}–{formatBrusselsTime(match.endsAt)}
                </p>
                <div className="public-match-players">
                  <span className="muted">Already in:</span>
                  {match.participants.length === 0 ? (
                    <span className="muted">No members joined yet.</span>
                  ) : (
                    <ul>
                      {match.participants.map((participant) => (
                        <li key={participant.memberId}>{participant.displayName} ({participant.matricule})</li>
                      ))}
                    </ul>
                  )}
                </div>
                <span className="muted">{match.availablePlaces} open place{match.availablePlaces === 1 ? "" : "s"}</span>
              </div>
              <button
                className="button"
                type="button"
                disabled={joiningId !== null || isCurrentMemberJoined || match.availablePlaces <= 0}
                onClick={() => void handleJoin(match)}
              >
                {buttonLabel}
              </button>
            </article>
          );
        })}
      </div>
    </section>
  );
}
