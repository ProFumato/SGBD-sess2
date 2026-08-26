import { Link, Navigate, Route, Routes } from "react-router-dom";
import { AppErrorBoundary } from "./components/ErrorBoundary";
import { ErrorState } from "./components/Feedback";
import { IdentityPage } from "./components/IdentityPage";
import { IdentityProvider, useIdentity } from "./state/identity";

function App() {
  return (
    <IdentityProvider>
      <AppErrorBoundary>
        <AppShell />
      </AppErrorBoundary>
    </IdentityProvider>
  );
}

function AppShell() {
  const { identity, clearIdentity } = useIdentity();

  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Padel Court Management</p>
          <h1>Club operations</h1>
        </div>
        {identity && (
          <button className="button button-secondary" type="button" onClick={clearIdentity}>
            Change member
          </button>
        )}
      </header>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/identity" element={<IdentityPage />} />
          <Route path="/member" element={<MemberGuard />} />
          <Route path="/admin" element={<AdminGuard />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
      <footer className="app-footer">
        <span>Identity uses matricule only; no password is required.</span>
        <span>Times are shown in Europe/Brussels.</span>
      </footer>
    </div>
  );
}

function MemberGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  return (
    <section className="content-card">
      <p className="eyebrow">Member area</p>
      <h2>Welcome, {identity.member.displayName}.</h2>
      <p className="muted">
        {identity.member.matricule} · {identity.member.membershipCategory} member
      </p>
    </section>
  );
}

function AdminGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  if (!identity.administratorRole) return <Navigate to="/member" replace />;
  return (
    <section className="content-card">
      <p className="eyebrow">Administrator area</p>
      <h2>Welcome, {identity.member.displayName}.</h2>
      <p className="muted">
        {identity.administratorRole.scope} administrator
        {identity.administratorRole.siteId ? ` · site ${identity.administratorRole.siteId}` : ""}
      </p>
    </section>
  );
}

function HomePage() {
  const { identity } = useIdentity();

  if (!identity) {
    return (
      <section className="hero-card">
        <p className="eyebrow">Frontend foundation</p>
        <h2>Manage reservations, matches, and club operations.</h2>
        <p>
          The member and administrator workflows will be added in separate vertical slices.
          Start with a matricule when the identity slice is implemented.
        </p>
        <Link className="button" to="/identity">
          Identify with matricule
        </Link>
      </section>
    );
  }

  return (
    <section className="content-card">
      <p className="eyebrow">Current identity</p>
      <h2>{identity.member.displayName}</h2>
      <p className="muted">
        {identity.member.matricule} · {identity.member.membershipCategory} member
      </p>
      <ErrorState>
        Member workflows are not enabled yet. Continue with the next frontend slice.
      </ErrorState>
    </section>
  );
}

function NotFoundPage() {
  return (
    <section className="content-card">
      <h2>Page not found</h2>
      <p>The requested frontend route does not exist.</p>
      <Link to="/">Return home</Link>
    </section>
  );
}

export default App;
