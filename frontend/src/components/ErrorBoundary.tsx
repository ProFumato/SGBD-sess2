import { Component, type ErrorInfo, type ReactNode } from "react";
import { ErrorState } from "./Feedback";

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  error: Error | null;
}

export class AppErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Frontend rendering error", error, errorInfo);
  }

  render() {
    if (this.state.error) {
      return (
        <section className="content-card" aria-labelledby="frontend-error-title">
          <p className="eyebrow">Unexpected error</p>
          <h2 id="frontend-error-title">This page could not be displayed.</h2>
          <ErrorState>{this.state.error.message}</ErrorState>
          <button className="button" type="button" onClick={() => window.location.reload()}>
            Reload page
          </button>
        </section>
      );
    }

    return this.props.children;
  }
}
