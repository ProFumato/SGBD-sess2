import { useEffect, useState } from "react";
import { Link, Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { getOutstandingDebts, type MemberDebt } from "./api/debt";
import { ApiError } from "./api/client";
import { formatBrusselsDateTime } from "./formatting/dateTime";
import { AppErrorBoundary } from "./components/ErrorBoundary";
import { ErrorState } from "./components/Feedback";
import { IdentityPage } from "./components/IdentityPage";
import { ReservationPage } from "./components/ReservationPage";
import { MatchParticipantsPage } from "./components/MatchParticipantsPage";
import { PublicMatchesPage } from "./components/PublicMatchesPage";
import { AdminMembersPage } from "./components/AdminMembersPage";
import { AdminConfigurationPage } from "./components/AdminConfigurationPage";
import { AdminStatisticsPage } from "./components/AdminStatisticsPage";
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

function AdminConfigurationGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  if (!identity.administratorRole) return <Navigate to="/member" replace />;
  return <AdminConfigurationPage />;
}

function AdminStatisticsGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  if (!identity.administratorRole) return <Navigate to="/member" replace />;
  return <AdminStatisticsPage />;
}

function AppShell() {
  const { identity, clearIdentity } = useIdentity();

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header-left">
          <BackButton />
          <div>
            <p className="eyebrow">Padel Court Management</p>
            <h1>Club operations</h1>
          </div>
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
          <Route path="/member/reservations" element={<MemberReservationGuard />} />
          <Route path="/member/matches" element={<MemberMatchGuard />} />
          <Route path="/member/public-matches" element={<MemberPublicMatchesGuard />} />
          <Route path="/admin" element={<AdminGuard />} />
          <Route path="/admin/members" element={<AdminMembersGuard />} />
          <Route path="/admin/configuration" element={<AdminConfigurationGuard />} />
          <Route path="/admin/statistics" element={<AdminStatisticsGuard />} />
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

function BackButton() {
  const navigate = useNavigate();

  const handleBack = () => {
    if (window.history.length > 1) {
      navigate(-1);
      return;
    }

    navigate("/");
  };

  return (
    <button className="back-button" type="button" onClick={handleBack} aria-label="Go back to previous page">
      ← Back
    </button>
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
      <Link className="button" to="/member/reservations">
        Create a game
      </Link>
      <Link className="button button-secondary" to="/member/public-matches">
        Find and join public games
      </Link>
      <Link className="button button-secondary" to="/member/matches">
        My private games
      </Link>
      <DebtPanel matricule={identity.member.matricule} />
    </section>
  );
}

function DebtPanel({ matricule }: { matricule: string }) {
  const [debts, setDebts] = useState<MemberDebt[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void getOutstandingDebts(matricule)
      .then(setDebts)
      .catch((caughtError: unknown) => {
        setError(caughtError instanceof ApiError ? caughtError.message : "Debt information could not be loaded.");
      });
  }, [matricule]);

  if (error) return <ErrorState>{error}</ErrorState>;
  if (debts.length === 0) {
    return (
      <div className="debt-panel debt-panel-clear">
        <strong>No outstanding debt</strong>
        <span>Your matches are fully covered.</span>
      </div>
    );
  }

  const total = debts.reduce((sum, debt) => sum + debt.outstandingAmount, 0);
  return (
    <div className="debt-panel" aria-labelledby="debt-title">
      <strong id="debt-title">Outstanding debt: €{total.toFixed(2)}</strong>
      <p>
        This amount is created the day before a match when a private game has fewer than four confirmed players.
        It represents the missing €15 places and is automatically included in your next successful organizer payment.
      </p>
      <ul>
        {debts.map((debt) => (
          <li key={debt.debtId}>
            Match #{debt.matchId} · {debt.courtName} · {formatBrusselsDateTime(debt.startsAt)}:{" "}
            <strong>€{debt.outstandingAmount.toFixed(2)}</strong>
            <span className="muted"> (originally €{debt.initialAmount.toFixed(2)})</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function MemberReservationGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  return <ReservationPage />;
}

function MemberMatchGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  return <MatchParticipantsPage />;
}

function MemberPublicMatchesGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  return <PublicMatchesPage />;
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
      <Link className="button" to="/admin/members">Manage members and roles</Link>
      <Link className="button button-secondary" to="/admin/configuration">Manage sites and schedules</Link>
      <Link className="button button-secondary" to="/admin/statistics">View statistics</Link>
    </section>
  );
}

function AdminMembersGuard() {
  const { identity } = useIdentity();
  if (!identity || !identity.member.isActive) return <Navigate to="/identity" replace />;
  if (!identity.administratorRole) return <Navigate to="/member" replace />;
  return <AdminMembersPage />;
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
