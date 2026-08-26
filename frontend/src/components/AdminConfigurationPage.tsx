import { useEffect, useState } from "react";
import { ApiError } from "../api/client";
import {
  createClosure, createCourt, createSite, deleteClosure, deleteSchedule, getClosures, getCourts, getSchedules, getSites,
  setSchedule, updateCourt, updateSite, updateClosure, type Closure, type Court, type Schedule, type Site,
} from "../api/administration";
import { useIdentity } from "../state/identity";
import { ErrorState, LoadingState } from "./Feedback";

export function AdminConfigurationPage() {
  const { identity } = useIdentity();
  const actor = identity!.member.matricule;
  const global = identity!.administratorRole?.scope === "Global";
  const [sites, setSites] = useState<Site[]>([]);
  const [siteId, setSiteId] = useState<number | null>(null);
  const [courts, setCourts] = useState<Court[]>([]);
  const [schedules, setSchedules] = useState<Schedule[]>([]);
  const [closures, setClosures] = useState<Closure[]>([]);
  const [newSiteName, setNewSiteName] = useState("");
  const [selectedSiteName, setSelectedSiteName] = useState("");
  const [courtName, setCourtName] = useState("");
  const [year, setYear] = useState(new Date().getFullYear());
  const [opening, setOpening] = useState("09:00:00");
  const [closing, setClosing] = useState("22:00:00");
  const [reason, setReason] = useState("");
  const [closureScope, setClosureScope] = useState<"Global" | "Site">("Global");
  const [closureStart, setClosureStart] = useState(`${year}-01-01T00:00`);
  const [closureEnd, setClosureEnd] = useState(`${year}-01-02T00:00`);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    try {
      const loadedSites = await getSites(actor);
      setSites(loadedSites);
      const selected = siteId ?? loadedSites[0]?.siteId ?? null;
      setSiteId(selected);
      setSelectedSiteName(loadedSites.find((site) => site.siteId === selected)?.name ?? "");
      if (selected) {
        setCourts(await getCourts(actor, selected));
        setSchedules(await getSchedules(actor, selected));
      }
      setClosures(await getClosures(actor));
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "Configuration could not be loaded.");
    }
  }
  useEffect(() => { void refresh(); }, [actor]);
  useEffect(() => { if (siteId) void Promise.all([getCourts(actor, siteId).then(setCourts), getSchedules(actor, siteId).then(setSchedules)]); }, [actor, siteId]);

  async function action(work: () => Promise<unknown>) {
    setBusy(true); setError(null);
    try { await work(); await refresh(); } catch (caughtError) { setError(caughtError instanceof ApiError ? caughtError.message : "Configuration change failed."); } finally { setBusy(false); }
  }
  if (!sites.length && !error) return <LoadingState label="Loading site configuration..." />;
  return <section className="content-card" aria-labelledby="config-title">
    <p className="eyebrow">Administration</p><h2 id="config-title">Sites, courts, schedules, and closures</h2>
    <p className="muted">{global ? "Global scope" : `Site scope · site ${identity!.administratorRole?.siteId}`}</p>
    {error && <ErrorState>{error}</ErrorState>}
    <label htmlFor="config-site">Selected site</label>
    <select id="config-site" value={siteId ?? ""} disabled={busy} onChange={(event) => {
      const selected = Number(event.target.value);
      setSiteId(selected);
      setSelectedSiteName(sites.find((site) => site.siteId === selected)?.name ?? "");
    }}>{sites.map((site) => <option key={site.siteId} value={site.siteId}>{site.name} (#{site.siteId})</option>)}</select>
    {siteId && <><form onSubmit={(event) => { event.preventDefault(); if (selectedSiteName.trim()) void action(() => updateSite(actor, siteId, selectedSiteName.trim())); }}>
      <h3>Site details</h3><p className="muted">Rename the selected site.</p><label htmlFor="selected-site-name">Site name</label><input id="selected-site-name" value={selectedSiteName} disabled={busy} onChange={(event) => setSelectedSiteName(event.target.value)} /><button className="button" disabled={busy || !selectedSiteName.trim()}>Save site</button>
    </form><form onSubmit={(event) => { event.preventDefault(); if (courtName.trim()) void action(() => createCourt(actor, siteId, courtName.trim())); setCourtName(""); }}>
      <h3>Courts</h3><label htmlFor="court-name">New court name</label><input id="court-name" value={courtName} onChange={(event) => setCourtName(event.target.value)} /><button className="button" disabled={busy}>Create court</button>
    </form><div className="admin-member-list">{courts.map((court) => <div className="admin-member-row" key={court.courtId}><span>{court.name} · {court.isActive ? "Active" : "Inactive"}</span><button className="button button-secondary" disabled={busy} onClick={() => void action(() => updateCourt(actor, court.courtId, court.name, !court.isActive))}>{court.isActive ? "Deactivate" : "Reactivate"}</button></div>)}</div>
    <form onSubmit={(event) => { event.preventDefault(); if (opening < closing) void action(() => setSchedule(actor, siteId, year, opening, closing)); else setError("Opening time must be before closing time."); }}>
      <h3>Annual schedule</h3><label htmlFor="schedule-year">Year</label><input id="schedule-year" type="number" value={year} onChange={(event) => setYear(Number(event.target.value))} /><label htmlFor="opening">Opening</label><input id="opening" type="time" step="1" value={opening.slice(0, 5)} onChange={(event) => setOpening(`${event.target.value}:00`)} /><label htmlFor="closing">Closing</label><input id="closing" type="time" step="1" value={closing.slice(0, 5)} onChange={(event) => setClosing(`${event.target.value}:00`)} /><button className="button" disabled={busy}>Save schedule</button>
    </form><div>{schedules.map((schedule) => <div className="admin-member-row" key={schedule.siteAnnualScheduleId}>Year {schedule.calendarYear}: {schedule.openingTime}–{schedule.closingTime}<button className="button button-danger" onClick={() => void action(() => deleteSchedule(actor, siteId, schedule.calendarYear))}>Delete</button></div>)}</div></>}
    {global && <form onSubmit={(event) => { event.preventDefault(); if (newSiteName.trim()) void action(() => createSite(actor, newSiteName.trim())); setNewSiteName(""); }}>
      <h3>Create site</h3><p className="muted">Add a new site to the club.</p><label htmlFor="site-name">New site name</label><input id="site-name" value={newSiteName} disabled={busy} onChange={(event) => setNewSiteName(event.target.value)} /><button className="button" disabled={busy || !newSiteName.trim()}>Create site</button>
    </form>}
    <form onSubmit={(event) => { event.preventDefault(); if (reason.trim() && closureStart < closureEnd && (closureScope === "Global" || siteId)) void action(() => createClosure(actor, { scope: closureScope, siteId: closureScope === "Site" ? siteId : null, startsAt: closureStart, endsAt: closureEnd, reason })); else setError("Closure reason, scope, and interval must be valid."); setReason(""); }}>
      <h3>Closure</h3><label htmlFor="closure-scope">Scope</label><select id="closure-scope" value={closureScope} onChange={(event) => setClosureScope(event.target.value as "Global" | "Site")}><option value="Global">Global</option><option value="Site">Selected site</option></select><label htmlFor="closure-start">Starts</label><input id="closure-start" type="datetime-local" value={closureStart} onChange={(event) => setClosureStart(event.target.value)} /><label htmlFor="closure-end">Ends</label><input id="closure-end" type="datetime-local" value={closureEnd} onChange={(event) => setClosureEnd(event.target.value)} /><label htmlFor="closure-reason">Reason</label><input id="closure-reason" value={reason} onChange={(event) => setReason(event.target.value)} /><button className="button" disabled={busy || (!global && closureScope === "Global")}>Create closure</button>
    </form>
    <div className="admin-member-list">{closures.map((closure) => <div className="admin-member-row" key={closure.closureId}><span>{closure.scope} · {closure.reason} · {closure.startsAt}–{closure.endsAt}</span><div className="participant-actions"><button className="button button-secondary" onClick={() => { const nextReason = window.prompt("Closure reason", closure.reason); if (nextReason?.trim()) void action(() => updateClosure(actor, closure.closureId, { scope: closure.scope, siteId: closure.siteId, startsAt: closure.startsAt, endsAt: closure.endsAt, reason: nextReason.trim() })); }}>Edit</button><button className="button button-danger" onClick={() => void action(() => deleteClosure(actor, closure.closureId))}>Delete</button></div></div>)}</div>
  </section>;
}
