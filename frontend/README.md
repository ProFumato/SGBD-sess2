# Padel Court Management frontend

This is the separate React + TypeScript client for the ASP.NET Core API.

## Local setup

1. Install Node.js 20 or newer.
2. Run `npm install` from this directory.
3. Copy `.env.example` to `.env.local`; the blank API URL uses the Vite proxy to the local HTTPS API.
4. Run `npm run dev`.

The development proxy targets `http://localhost:5065`.
Set `VITE_API_BASE_URL` to an absolute API URL when using a deployed backend.

Available checks:

- `npm run build`
- `npm run lint`
- `npm test`

The application identifies members with a matricule only. It does not implement passwords or store API credentials.
