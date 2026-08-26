# Padel Court Management frontend

This is the separate React + TypeScript client for the ASP.NET Core API.

## Local setup

1. Install Node.js 20 or newer.
2. Run `npm install` from this directory.
3. Copy `.env.example` to `.env.local` when the API is not available at `http://localhost:5065`.
4. Run `npm run dev`.

Available checks:

- `npm run build`
- `npm run lint`
- `npm test`

The application identifies members with a matricule only. It does not implement passwords or store API credentials.
