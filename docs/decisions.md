# Technical decisions

## Application structure

- The client will use React and TypeScript.
- The backend will use ASP.NET Core and expose a REST API.
- The frontend and backend will remain in separate folders.

## Backend organisation

- `Domain` contains the business model and rules.
- `Application` contains use cases and interfaces.
- `Infrastructure` contains database access and other technical implementations.
- `Api` contains HTTP endpoints and dependency injection setup.

## Database

- SQL Server will be used for persistent data.
- Database changes will be stored as versioned SQL scripts.
- Database access will use separate credentials for schema changes and normal application use.

## Business rules

- Application services will handle workflow rules such as reservations and payments.
- Database constraints and transactions will protect data consistency.
- Time-based reservation and debt rules will be processed by a scheduled backend task.

## Payments

- Payments will be simulated internally unless an external provider is explicitly approved.
