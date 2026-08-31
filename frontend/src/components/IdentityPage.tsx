import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { identifyMember } from "../api/identity";
import { ApiError } from "../api/client";
import { ErrorState, LoadingState } from "./Feedback";
import { useIdentity } from "../state/identity";
import { matriculePattern } from "../validation/matricule";

export function IdentityPage() {
  const navigate = useNavigate();
  const { identity: currentIdentity, setIdentity } = useIdentity();
  const [matricule, setMatricule] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedMatricule = matricule.trim().toUpperCase();
    setMatricule(normalizedMatricule);
    if (!matriculePattern.test(normalizedMatricule)) {
      setError("Use a matricule in the format G0001, S00001, or L00001.");
      return;
    }

    setError(null);
    setIsLoading(true);
    try {
      const identity = await identifyMember(normalizedMatricule);
      setIdentity(identity);
      if (!identity.member.isActive) {
        setError("This member is inactive and cannot access member workflows.");
      } else {
        navigate(identity.administratorRole ? "/admin" : "/member", { replace: true });
      }
    } catch (caughtError) {
      if (caughtError instanceof DOMException && caughtError.name === "AbortError") return;
      if (caughtError instanceof ApiError) {
        setError(
          caughtError.status === 404
            ? "No member was found for this matricule."
            : caughtError.status === 403
              ? "This member is not allowed to access the application."
              : caughtError.message,
        );
      } else {
        setError("The member could not be identified. Check the API connection and try again.");
      }
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <section className="content-card" aria-labelledby="identity-title">
      <p className="eyebrow">Member identification</p>
      <h2 id="identity-title">Enter your matricule</h2>
      <p className="muted">This identifies you for the session. It is not a password or account.</p>
      <form onSubmit={handleSubmit} noValidate>
        <label htmlFor="matricule">Matricule</label>
        <input
          id="matricule"
          name="matricule"
          value={matricule}
          onChange={(event) => setMatricule(event.target.value)}
          autoComplete="off"
          autoCapitalize="characters"
          aria-invalid={error ? "true" : "false"}
          disabled={isLoading}
        />
        <button className="button" type="submit" disabled={isLoading}>
          Identify member
        </button>
      </form>
      {currentIdentity && (
        <div className="identity-summary">
          <strong>{currentIdentity.member.displayName}</strong>
          <span>
            {currentIdentity.member.matricule} ·{" "}
            {currentIdentity.member.isActive ? "Active member" : "Inactive member"}
          </span>
        </div>
      )}
      {isLoading && <LoadingState label="Looking up member..." />}
      {error && <ErrorState>{error}</ErrorState>}
    </section>
  );
}
