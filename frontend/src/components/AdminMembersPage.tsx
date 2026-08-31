import { useEffect, useMemo, useState, type FormEvent } from "react";
import { ApiError } from "../api/client";
import {
  createMember,
  getMembers,
  getSites,
  removeAdministratorRole,
  setAdministratorRole,
  setMemberActivation,
  updateMember,
  type AdminMember,
  type AdministratorScope,
  type MembershipCategory,
  type Site,
} from "../api/administration";
import { useIdentity } from "../state/identity";
import { ErrorState, LoadingState } from "./Feedback";
import { clearMemberDebts, getMemberDebts } from "../api/debt";

const emptyMember = {
  matricule: "",
  displayName: "",
  membershipCategory: "Free" as MembershipCategory,
  homeSiteId: null as number | null,
  isActive: true,
};

export function AdminMembersPage() {
  const { identity } = useIdentity();
  const actor = identity!.member.matricule;
  const isGlobalAdmin = identity!.administratorRole?.scope === "Global";
  const [members, setMembers] = useState<AdminMember[]>([]);
  const [sites, setSites] = useState<Site[]>([]);
  const [siteQuery, setSiteQuery] = useState("");
  const [query, setQuery] = useState("");
  const [form, setForm] = useState(emptyMember);
  const [editing, setEditing] = useState<string | null>(null);
  const [roleMatricule, setRoleMatricule] = useState("");
  const [roleScope, setRoleScope] = useState<AdministratorScope>("Site");
  const [roleSiteId, setRoleSiteId] = useState("");
  const [roleSiteQuery, setRoleSiteQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    setLoading(true);
    try {
      const [loadedMembers, loadedSites] = await Promise.all([getMembers(actor), getSites(actor)]);
      setMembers(loadedMembers);
      setSites(loadedSites);
      setError(null);
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "Members or sites could not be loaded.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, [actor]);

  async function saveMember(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const input = { ...form, homeSiteId: form.homeSiteId || null };
      if (editing) await updateMember(actor, editing, input);
      else await createMember(actor, input);
      setForm(emptyMember);
      setEditing(null);
      await refresh();
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "The member could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  async function toggleMember(member: AdminMember) {
    setBusy(true);
    setError(null);
    try {
      await setMemberActivation(actor, member.matricule, !member.isActive);
      await refresh();
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "The member status could not be changed.");
    } finally {
      setBusy(false);
    }
  }

  async function inspectDebt(member: AdminMember) {
      setBusy(true);
      setError(null);
      try {
        const debts = await getMemberDebts(actor, member.matricule);
        const total = debts.reduce((sum, debt) => sum + debt.outstandingAmount, 0);
        window.alert(
          total > 0
            ? `${member.displayName} has €${total.toFixed(2)} outstanding debt across ${debts.length} match(es).`
            : `${member.displayName} has no outstanding debt.`,
        );
      } catch (caughtError) {
        setError(caughtError instanceof ApiError ? caughtError.message : "The member debt could not be loaded.");
      } finally {
        setBusy(false);
      }
    }

  async function clearDebt(member: AdminMember) {
      if (!window.confirm(`Remove all outstanding debt for ${member.displayName}?`)) return;
      setBusy(true);
      setError(null);
      try {
        await clearMemberDebts(actor, member.matricule);
      } catch (caughtError) {
        setError(caughtError instanceof ApiError ? caughtError.message : "The member debt could not be removed.");
      } finally {
        setBusy(false);
    }
  }

  async function assignRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!isGlobalAdmin || !roleMatricule.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await setAdministratorRole(actor, roleMatricule.trim().toUpperCase(), roleScope, roleScope === "Site" ? Number(roleSiteId) : null);
      setRoleMatricule("");
      setRoleSiteId("");
      setRoleSiteQuery("");
      await refresh();
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "The administrator role could not be changed.");
    } finally {
      setBusy(false);
    }
  }

  const visibleMembers = members.filter((member) =>
    `${member.matricule} ${member.displayName}`.toLowerCase().includes(query.toLowerCase()),
  );
  const visibleSites = useMemo(
    () => sites.filter((site) => site.name.toLowerCase().includes(siteQuery.toLowerCase())),
    [siteQuery, sites],
  );
  const visibleRoleSites = useMemo(
    () => sites.filter((site) => site.name.toLowerCase().includes(roleSiteQuery.toLowerCase())),
    [roleSiteQuery, sites],
  );

  if (loading) return <LoadingState label="Loading administrator members..." />;
  return (
    <section className="content-card" aria-labelledby="admin-members-title">
      <p className="eyebrow">Administration</p>
      <h2 id="admin-members-title">Members and roles</h2>
      <p className="muted">{isGlobalAdmin ? "Global administrator scope" : `Site administrator scope · site ${identity!.administratorRole?.siteId}`}</p>
      {error && <ErrorState>{error}</ErrorState>}
      <label htmlFor="member-search">Search members</label>
      <input id="member-search" value={query} onChange={(event) => setQuery(event.target.value)} />
      <div className="admin-member-list">
        {visibleMembers.map((member) => (
          <article className="admin-member-row" key={member.memberId}>
            <div>
              <strong>{member.displayName}</strong>
              <span>{member.matricule} · {member.membershipCategory} · {member.isActive ? "Active" : "Inactive"}</span>
            </div>
            <div className="participant-actions">
              <button className="button button-secondary" type="button" onClick={() => {
                setEditing(member.matricule);
                setForm({ matricule: member.matricule, displayName: member.displayName, membershipCategory: member.membershipCategory, homeSiteId: member.homeSiteId, isActive: member.isActive });
              }}>Edit</button>
              <button className="button button-secondary" type="button" disabled={busy} onClick={() => void toggleMember(member)}>
                {member.isActive ? "Deactivate" : "Reactivate"}
              </button>
              <button className="button button-secondary" type="button" disabled={busy} onClick={() => void inspectDebt(member)}>
                Check debt
              </button>
              <button className="button button-danger" type="button" disabled={busy} onClick={() => void clearDebt(member)}>
                Remove debt
              </button>
            </div>
          </article>
        ))}
      </div>
      <form onSubmit={saveMember}>
        <h3>{editing ? `Edit ${editing}` : "Create member"}</h3>
        <label htmlFor="member-matricule">Matricule</label>
        <input id="member-matricule" value={form.matricule} disabled={Boolean(editing) || busy} onChange={(event) => setForm({ ...form, matricule: event.target.value.toUpperCase() })} />
        <label htmlFor="member-name">Display name</label>
        <input id="member-name" value={form.displayName} disabled={busy} onChange={(event) => setForm({ ...form, displayName: event.target.value })} />
        <label htmlFor="member-category">Category</label>
        <select id="member-category" value={form.membershipCategory} disabled={busy} onChange={(event) => {
          const membershipCategory = event.target.value as MembershipCategory;
          setForm({ ...form, membershipCategory, homeSiteId: membershipCategory === "Site" ? form.homeSiteId : null });
        }}>
          <option value="Global">Global</option><option value="Site">Site</option><option value="Free">Free</option>
        </select>
        {form.membershipCategory === "Site" && <>
          <label htmlFor="member-site-search">Home site</label>
          <input id="member-site-search" placeholder="Search sites by name" value={siteQuery} disabled={busy} onChange={(event) => setSiteQuery(event.target.value)} />
          <select
            id="member-home-site"
            value={form.homeSiteId ?? ""}
            disabled={busy || visibleSites.length === 0}
            onChange={(event) => setForm({ ...form, homeSiteId: Number(event.target.value) || null })}
          >
            <option value="">Select a site</option>
            {visibleSites.map((site) => <option key={site.siteId} value={site.siteId}>{site.name} (#{site.siteId})</option>)}
          </select>
          {sites.length === 0 && <span className="muted">No sites are available in your administration scope.</span>}
        </>}
        <button className="button" type="submit" disabled={busy || !form.matricule || !form.displayName}>{editing ? "Save member" : "Create member"}</button>
      </form>
      {isGlobalAdmin && <form onSubmit={assignRole}>
        <h3>Administrator role</h3>
        <label htmlFor="role-matricule">Member matricule</label>
        <input id="role-matricule" value={roleMatricule} disabled={busy} onChange={(event) => setRoleMatricule(event.target.value.toUpperCase())} />
        <label htmlFor="role-scope">Scope</label>
        <select id="role-scope" value={roleScope} disabled={busy} onChange={(event) => {
          const scope = event.target.value as AdministratorScope;
          setRoleScope(scope);
          if (scope === "Global") setRoleSiteId("");
        }}>
          <option value="Global">Global</option><option value="Site">Site</option>
        </select>
        {roleScope === "Site" && <>
          <label htmlFor="role-site-search">Administrator site</label>
          <input id="role-site-search" placeholder="Search sites by name" value={roleSiteQuery} disabled={busy} onChange={(event) => setRoleSiteQuery(event.target.value)} />
          <select
            id="role-site"
            value={roleSiteId}
            disabled={busy || visibleRoleSites.length === 0}
            onChange={(event) => setRoleSiteId(event.target.value)}
          >
            <option value="">Select a site</option>
            {visibleRoleSites.map((site) => <option key={site.siteId} value={site.siteId}>{site.name} (#{site.siteId})</option>)}
          </select>
          {sites.length === 0 && <span className="muted">No sites are available in your administration scope.</span>}
        </>}
        <button className="button" type="submit" disabled={busy || !roleMatricule || (roleScope === "Site" && !roleSiteId)}>Assign role</button>
      </form>}
      {isGlobalAdmin && <form onSubmit={(event) => {
        event.preventDefault();
        if (!roleMatricule.trim()) return;
        if (window.confirm(`Remove administrator role from ${roleMatricule}?`)) {
          setBusy(true);
          void removeAdministratorRole(actor, roleMatricule.trim().toUpperCase()).then(refresh).catch((caughtError: unknown) => setError(caughtError instanceof ApiError ? caughtError.message : "The administrator role could not be removed.")).finally(() => setBusy(false));
        }
      }}>
        <button className="button button-danger" type="submit" disabled={busy || !roleMatricule}>Remove selected role</button>
      </form>}
    </section>
  );
}
