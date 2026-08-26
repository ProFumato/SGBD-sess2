import { useEffect, useState } from "react";
import { ApiError } from "../api/client";
import {
  createMember,
  getMembers,
  removeAdministratorRole,
  setAdministratorRole,
  setMemberActivation,
  updateMember,
  type AdminMember,
  type AdministratorScope,
  type MembershipCategory,
} from "../api/administration";
import { useIdentity } from "../state/identity";
import { ErrorState, LoadingState } from "./Feedback";

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
  const [query, setQuery] = useState("");
  const [form, setForm] = useState(emptyMember);
  const [editing, setEditing] = useState<string | null>(null);
  const [roleMatricule, setRoleMatricule] = useState("");
  const [roleScope, setRoleScope] = useState<AdministratorScope>("Site");
  const [roleSiteId, setRoleSiteId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    setLoading(true);
    try {
      setMembers(await getMembers(actor));
      setError(null);
    } catch (caughtError) {
      setError(caughtError instanceof ApiError ? caughtError.message : "Members could not be loaded.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, [actor]);

  async function saveMember(event: React.FormEvent<HTMLFormElement>) {
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

  async function assignRole(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!isGlobalAdmin || !roleMatricule.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await setAdministratorRole(actor, roleMatricule.trim().toUpperCase(), roleScope, roleScope === "Site" ? Number(roleSiteId) : null);
      setRoleMatricule("");
      setRoleSiteId("");
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
        <select id="member-category" value={form.membershipCategory} disabled={busy} onChange={(event) => setForm({ ...form, membershipCategory: event.target.value as MembershipCategory })}>
          <option value="Global">Global</option><option value="Site">Site</option><option value="Free">Free</option>
        </select>
        <label htmlFor="member-home-site">Home site ID</label>
        <input id="member-home-site" type="number" min="1" value={form.homeSiteId ?? ""} disabled={busy} onChange={(event) => setForm({ ...form, homeSiteId: Number(event.target.value) || null })} />
        <button className="button" type="submit" disabled={busy || !form.matricule || !form.displayName}>{editing ? "Save member" : "Create member"}</button>
      </form>
      {isGlobalAdmin && <form onSubmit={assignRole}>
        <h3>Administrator role</h3>
        <label htmlFor="role-matricule">Member matricule</label>
        <input id="role-matricule" value={roleMatricule} onChange={(event) => setRoleMatricule(event.target.value)} />
        <label htmlFor="role-scope">Scope</label>
        <select id="role-scope" value={roleScope} onChange={(event) => setRoleScope(event.target.value as AdministratorScope)}>
          <option value="Global">Global</option><option value="Site">Site</option>
        </select>
        {roleScope === "Site" && <><label htmlFor="role-site">Site ID</label><input id="role-site" type="number" min="1" value={roleSiteId} onChange={(event) => setRoleSiteId(event.target.value)} /></>}
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
