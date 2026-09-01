# Padel Court Management

Projet scolaire de gestion de terrains de padel. Il permet notamment de consulter les disponibilites, creer des reservations privees ou publiques, gerer les participants, enregistrer des paiements et appliquer les regles de traitement de la veille.

Le projet est separe en trois parties :

- `backend/` : API REST ASP.NET Core et logique metier.
- `frontend/` : interface web React + TypeScript.
- `database/` : scripts SQL Server pour la base, les donnees initiales et les permissions.

> Les commentaires du code et l'interface web sont en anglais. Le francais est ma troisieme langue, donc l'anglais a ete garde pour rester coherent avec les outils et le code.

## Prerequis

- SQL Server
- .NET SDK 10
- Node.js 20 ou plus recent

## Lancer la base de donnees

Executer les scripts du dossier `database/` dans cet ordre :

1. `1-create-database.sql`
2. `2-create-schema.sql`
3. `3-seed-data.sql`
4. `4-create-security.sql`
5. `5-administration-integrity.sql`

Le script de securite cree les utilisateurs de base de donnees mais attend que les logins SQL Server `PadelCourtAppLogin` et `PadelCourtSchemaLogin` existent deja sur le serveur.

## Lancer le backend

Configurer une chaine de connexion vers SQL Server avec les user secrets :

```bash
dotnet user-secrets set "ConnectionStrings:PadelCourtManagement" "Server=localhost;Database=PadelCourtManagement;Trusted_Connection=True;TrustServerCertificate=True" --project backend/src/PadelCourtManagement.Api
```

Adapter cette chaine de connexion selon votre instance SQL Server, puis lancer l'API depuis la racine du projet :

```bash
dotnet run --project backend/src/PadelCourtManagement.Api
```

Swagger est disponible en environnement `Development`.

## Lancer le frontend

Dans un second terminal :

```bash
cd frontend
npm install
cp .env.example .env.local
npm run dev
```

Quand `VITE_API_BASE_URL` reste vide, Vite transfere les appels `/api` vers l'API locale sur `http://localhost:5065`.

## Tests

Les commandes principales sont :

```bash
dotnet test PadelCourtManagement.slnx
cd frontend && npm test
```
