/**
 * Admin Statistics Page
 * View usage reports and statistics for a selected date range and site.
 */

import { useEffect, useState } from "react";
import { ApiError } from "../api/client";
import { getSites, getStatistics, type Site, type StatisticsReport } from "../api/administration";
import { useIdentity } from "../state/identity";
import { EmptyState, ErrorState, LoadingState } from "./Feedback";

function toDateInput(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

export function AdminStatisticsPage() {
  const { identity } = useIdentity();
  const actor = identity!.member.matricule;
  const global = identity!.administratorRole?.scope === "Global";
  const today = new Date();
  const [from, setFrom] = useState(toDateInput(today));
  const [to, setTo] = useState(toDateInput(new Date(today.getTime() + 30 * 86400000)));
  const [siteId, setSiteId] = useState("");
  const [sites, setSites] = useState<Site[]>([]);
  const [report, setReport] = useState<StatisticsReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function loadReport() {
    if (!from || !to || from > to) {
      setError("The statistics start date must be before or equal to the end date.");
      setReport(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const result = await getStatistics(actor, `${from}T00:00:00`, `${to}T23:59:59`, siteId ? Number(siteId) : undefined);
      setReport(result);
    } catch (caughtError) {
      setReport(null);
      setError(caughtError instanceof ApiError ? caughtError.message : "Statistics could not be loaded.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    setLoading(true);
    setError(null);
    void Promise.all([
      getSites(actor),
      getStatistics(actor, `${from}T00:00:00`, `${to}T23:59:59`),
    ]).then(([loadedSites, loadedReport]) => {
      setSites(loadedSites);
      setReport(loadedReport);
    }).catch((caughtError: unknown) => {
      setError(caughtError instanceof ApiError ? caughtError.message : "Statistics could not be loaded.");
    }).finally(() => setLoading(false));
  }, [actor]);

  return (
    <section className="content-card statistics-card" aria-labelledby="statistics-title">
      <p className="eyebrow">Administration</p>
      <h2 id="statistics-title">Statistics</h2>
      <p className="muted">{global ? "Global scope" : `Site scope · site ${identity!.administratorRole?.siteId}`}</p>
      <form onSubmit={(event) => { event.preventDefault(); void loadReport(); }}>
        <label htmlFor="statistics-from">From</label>
        <input id="statistics-from" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        <label htmlFor="statistics-to">To</label>
        <input id="statistics-to" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        <label htmlFor="statistics-site">Site filter</label>
        <select id="statistics-site" value={siteId} onChange={(event) => setSiteId(event.target.value)}>
          <option value="">All permitted sites</option>
          {sites.map((site) => <option key={site.siteId} value={site.siteId}>{site.name} (#{site.siteId})</option>)}
        </select>
        <button className="button" type="submit" disabled={loading}>Load statistics</button>
      </form>
      {error && <ErrorState>{error}</ErrorState>}
      {loading && <LoadingState label="Loading statistics..." />}
      {report && <>
        <div className="statistics-grid">
          <div><strong>Revenue</strong><span>€{report.revenue.toFixed(2)}</span></div>
          <div><strong>Matches</strong><span>{report.matches}</span></div>
          <div><strong>Confirmed places</strong><span>{report.confirmedParticipations}</span></div>
          <div><strong>Total capacity</strong><span>{report.capacity}</span></div>
          <div><strong>Active members</strong><span>{report.activeMembers}</span></div>
          <div><strong>Outstanding debt</strong><span>€{report.outstandingDebt.toFixed(2)}</span></div>
          <div><strong>Active booking bans</strong><span>{report.activeBookingBans}</span></div>
        </div>
        {report.breakdown.length === 0 ? <EmptyState>No matches were found for this scope and date range.</EmptyState> : (
          <div className="statistics-table-wrap">
            <table>
              <caption>Site and court breakdown</caption>
              <thead><tr><th>Site</th><th>Court</th><th>Matches</th><th>Confirmed</th><th>Revenue</th></tr></thead>
              <tbody>{report.breakdown.map((item) => (
                <tr key={`${item.siteId}-${item.courtId}`}><td>{item.siteName}</td><td>{item.courtName}</td><td>{item.matches}</td><td>{item.confirmedParticipations}</td><td>€{item.revenue.toFixed(2)}</td></tr>
              ))}</tbody>
            </table>
          </div>
        )}
      </>}
    </section>
  );
}
