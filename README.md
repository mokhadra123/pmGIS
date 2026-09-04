# pmGIS

A project management application with a map. Project attributes, activities and audit
fields live in PostgreSQL (PostGIS enabled). The point geometry for each project lives in a
hosted ArcGIS Feature Layer. The two stores are linked by the feature's `OBJECTID`, which is
stored on the project row as `Project.ObjectId`. The server owns both writes and orders them
so a failure on either side does not leave an orphan.

Design decisions and their reasoning are recorded in [ASSUMPTIONS.md](ASSUMPTIONS.md). This
file covers setup, configuration, the layer schema, the API surface and a demo walkthrough.

## Architecture

Five .NET 10 projects and one Angular application.

```
PMGIS.AppHost         Aspire orchestration: postgres container, api, client
  |
  +-- PMGIS.Api ------------------ ASP.NET Core minimal API, vertical slices
        |     \
        |      +-- PMGIS.ServiceDefaults   health checks, OpenTelemetry, resilience
        |
        +-- PMGIS.Infrastructure --------- EF Core, ArcGIS feature service client, seeder
              |
              +-- PMGIS.Domain ----------- entities, enums, rules. No dependencies.

client/               Angular 21, @arcgis/core map, dev server proxies /api
```

Dependency direction: `Domain` references nothing. `Infrastructure` references `Domain`.
`Api` references `Domain`, `Infrastructure` and `ServiceDefaults`. `AppHost` references `Api`
only, for the Aspire project reference; it is not on the runtime path of the API.

The API is organised as vertical slices under `PMGIS.Api/Features/<Area>/<Slice>/`. Each
slice owns its query or command, its handler, its validator and its endpoint. Slices are
registered explicitly in `ProjectsFeature.cs`, `GisFeature.cs` and `LookupsFeature.cs` — there
is no assembly scanning.

The Angular client is a single workspace route: a list pane, a map pane, and a router outlet
that opens as a detail pane for `projects/new`, `projects/:id` and `projects/:id/edit`.

## Prerequisites

| Requirement | Version | Notes |
| --- | --- | --- |
| .NET SDK | 10.0 | All projects target `net10.0`. Verified on 10.0.302. |
| Docker Desktop | running | Aspire starts a `postgis/postgis:16-3.4` container. |
| Node.js | 22.12+ or 24.x | Angular 21 toolchain. Verified on 24.13.1. |
| npm | 11.x | `client/package.json` pins `npm@11.8.0` via `packageManager`. |
| dotnet-ef | 10.x | Only needed to add or apply migrations by hand. |

```bash
dotnet tool install --global dotnet-ef --version 10.*
```

There is no `global.json`, so the SDK is whatever `dotnet --version` resolves to.

## Getting started

1. Set the Postgres password. The AppHost declares it as an Aspire parameter named
   `postgres-password`, read from configuration under `Parameters:postgres-password`. It has no
   default, so the AppHost will not start without it.

   ```bash
   cd server
   dotnet user-secrets set "Parameters:postgres-password" "postgres" --project PMGIS.AppHost
   ```

   Use `postgres` unless you have a reason not to — the EF design-time factory falls back to
   that same password (see [Database](#database)).

2. Start everything.

   ```bash
   cd server
   dotnet run --project PMGIS.AppHost
   ```

   This starts the Postgres container, the API, and the Angular dev server as three Aspire
   resources.

3. Wait. On a fresh clone the `client` resource runs `npm install` as its own Aspire resource
   before `ng serve` starts, so the client sits in **Waiting** in the dashboard for a couple of
   minutes with no output. This is expected and is not a hang. It only happens when
   `client/node_modules` is absent or stale.

| What | Where |
| --- | --- |
| Aspire dashboard | `https://localhost:17004` (printed on the console with a login token) |
| Angular client | `http://localhost:4200` |
| API | port assigned by Aspire; open it from the dashboard |
| API reference (Scalar) | `<api>/scalar` — development only |
| OpenAPI document | `<api>/openapi/v1.json` — development only |
| Health | `<api>/health` and `<api>/alive` — development only |

The API's port is assigned by Aspire, not fixed. The client never needs to know it: the app
calls `/api` on its own origin and `client/proxy.conf.mjs` forwards it to whatever address
Aspire published, read from the `services__api__https__0` / `services__api__http__0`
environment variables.

### Running the client on its own

```bash
cd client
npm install
npm start
```

With no Aspire variables present the proxy falls back to `http://localhost:5055`, which is the
API's `http` launch profile. So `dotnet run --project PMGIS.Api --launch-profile http` from
`server/` pairs with it — but the API then needs a reachable `pmgisdb` connection string,
which Aspire normally supplies.

## Configuration

### API — `server/PMGIS.Api/appsettings.json`

```json
{
  "Cors": { "Origins": [ "http://localhost:4200" ] },
  "ArcGis": {
    "FeatureLayerUrl": "https://services3.arcgis.com/GVgbJbqm8hXASVYi/ArcGIS/rest/services/my_points/FeatureServer/0",
    "TokenUrl": "https://www.arcgis.com/sharing/rest/generateToken",
    "SourceIdBase": 5000000,
    "CodeField": "name",
    "SourceIdField": "SOURCEID",
    "QueryPageSize": 1000
  }
}
```

| Key | Meaning |
| --- | --- |
| `Cors:Origins` | Allowed browser origins. `Content-Disposition` is exposed so the CSV export can read its filename. Defaults to `http://localhost:4200` if the section is missing. |
| `ArcGis:FeatureLayerUrl` | Full layer URL, ending in the layer index. |
| `ArcGis:TokenUrl` | Token endpoint, used only if credentials are set. |
| `ArcGis:Username` / `Password` | Not set. The layer is used anonymously; the token provider returns null and no token is sent. Set both to point the app at a secured layer — no code change. |
| `ArcGis:SourceIdBase` | `5000000`. Features this app owns carry `SOURCEID >= 5000000`. |
| `ArcGis:CodeField` | `name` — the layer attribute that holds the Project Code. |
| `ArcGis:SourceIdField` | `SOURCEID`. |
| `ArcGis:QueryPageSize` | `1000`, under the layer's `maxRecordCount` of 10000. |

Do not put credentials in `appsettings.json`. Use user secrets on `PMGIS.Api` if you need them.

### AppHost — user secrets

| Key | Purpose |
| --- | --- |
| `Parameters:postgres-password` | Password for the `postgres` container and the `pmgisdb` connection string. Required. |

### Client — `client/src/app/core/config/app-config.ts`

`APP_CONFIG` is a plain compile-time constant, not a runtime settings file.

| Key | Value | Notes |
| --- | --- | --- |
| `api.baseUrl` | `/api` | Same origin; the dev proxy handles the rest. |
| `arcgis.projectLayerURL` | the layer URL | **Must match `ArcGis:FeatureLayerUrl`.** |
| `arcgis.sourceIdBase` | `5_000_000` | **Must match `ArcGis:SourceIdBase`.** The client uses it as the layer's definition expression; if the two drift, the map draws points the server does not consider its own, or hides points it created. |
| `arcgis.searchField` | `name` | Backs the map Search widget. Must match `ArcGis:CodeField`. |
| `arcgis.reverseGeocodeURL` | World GeocodeServer | Anonymous, decorative. Failures are swallowed. |
| `map.*` | centre, zoom, cluster scale | Initial view is Cairo at zoom 10. |
| `boundary` | `Egypt`, `[24.7, 21.7, 36.9, 31.7]` | The allowed-location envelope enforced on the picked point. |
| `list.pageSize` / `searchDebounceMS` | `25` / `300` | Grid page size and search debounce. |
| `form.autosaveIntervalMS` / `draftStorageKey` | `30_000` / `pmgis.project-form.draft` | Local-storage draft autosave. |

## Database

PostGIS 16-3.4 in a container, on a **pinned host port 5432** with a persistent data volume
(`pmgis-pgdata`) and `ContainerLifetime.Persistent`. The port is pinned so the EF tooling has a
stable target; the volume and lifetime mean the container and its data survive an AppHost
restart.

Migrations live in `PMGIS.Infrastructure/Data/Migrations`. `PMGIS.Infrastructure` is both the
migrations project and its own startup project, via `DesignTimeDbContextFactory`:

```bash
cd server
dotnet ef migrations add <Name> \
  --project PMGIS.Infrastructure --startup-project PMGIS.Infrastructure

dotnet ef database update \
  --project PMGIS.Infrastructure --startup-project PMGIS.Infrastructure
```

The factory reads `PMGIS_MIGRATIONS_CONNECTION` and falls back to:

```
Host=localhost;Port=5432;Database=pmgisdb;Username=postgres;Password=postgres
```

So if your `postgres-password` secret is anything other than `postgres`, override it:

```bash
PMGIS_MIGRATIONS_CONNECTION="Host=localhost;Port=5432;Database=pmgisdb;Username=postgres;Password=<yours>" \
  dotnet ef database update --project PMGIS.Infrastructure --startup-project PMGIS.Infrastructure
```

You normally do not need any of this. At startup the API calls `Database.MigrateAsync()` and
then seeds, so a fresh database is created and populated by `dotnet run --project PMGIS.AppHost`
alone.

### Seeding

`DataSeeder` runs **only when `ASPNETCORE_ENVIRONMENT=Development`** and is idempotent: it
returns immediately if any project row exists. It creates 25 users, 7 project types
(INFRA, WATER, ROAD, ENERGY, BUILD, ENV, TELECOM) and 5,000 projects with activities, in
batches of 500. The Bogus randomizer is fixed to seed `20260904`, so two fresh runs produce
identical data. Coordinates are scattered around eight Egyptian city anchors.

Seeded projects have `ObjectId = NULL` — no features are written to the shared ArcGIS layer by
the seed. See [Known limitations](#known-limitations).

To reseed from scratch, stop the AppHost, remove the `pmgis-pgdata` Docker volume, and start
again.

## Feature layer attribute schema

Layer: `.../my_points/FeatureServer/0`. Geometry is a single point, WGS84 (`wkid` 4326).
`maxRecordCount` is 10000. The layer is a shared public sample with a fixed schema; this
application chose which of its existing fields to use rather than designing new ones.

| Field | Esri type | Length | Nullable | Used for |
| --- | --- | --- | --- | --- |
| `OBJECTID` | `esriFieldTypeOID` | — | no | Assigned by the service on create. Stored on the project row as `Project.ObjectId`; this is the link between the two stores. |
| `name` | `esriFieldTypeString` | 256 | yes | The **Project Code** (`ABC-0000`), not the project's display name. Search widget field, popup title, and how a clicked point is resolved back to a project row. |
| `SOURCEID` | `esriFieldTypeInteger` | — | yes | `5000000 + projectId`. Namespaces this application's features inside the shared layer. |
| `rating` | `esriFieldTypeString` | 256 | yes | Present on the layer. **Not read or written by this application.** |
| geometry | point, WGS84 | — | — | The project's location. Longitude/latitude are also mirrored onto the project row. |

`outFields` on both client and server are limited to `OBJECTID`, `name`, `SOURCEID`.

### Why the definition expression

The layer is public and other consumers write to it. Every read the application makes — the
map layer, the popup lookups, the reconciliation report — applies:

```
SOURCEID >= 5000000
```

Without it, every feature written by anyone else would be drawn on the map and reported as an
orphan by the reconciliation check, making that check useless. `SOURCEID` is derived from the
project id, so the single-create path and the bulk backfill compute the same value for the same
project, which is what lets an interrupted backfill find a feature it already created instead of
duplicating it.

The base is `5000000` rather than the `900000` used earlier because another consumer of the same
public layer had taken that range: the map drew thousands of their points as projects. A range on
a public layer is a convention and not a guarantee, so the backfill adopts a feature only when its
`name` also carries the project's code — see ASSUMPTIONS.md.

`name` carries the Project Code rather than the project name because it is the layer's only
searchable text field and the code is the unique, stable identifier of the two. The layer holds
no copy of the project's name, type or dates — the popup fetches those from the API.

## API reference

All routes are under `/api`. Full request and response shapes are in Scalar at `<api>/scalar`.

### Projects

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/projects` | Filtered, sorted, paged list. Query: `page`, `pageSize`, `sort`, `dir`, `search`, `typeIds`, `statuses`, `dateFrom`, `dateTo`, `minLon`/`minLat`/`maxLon`/`maxLat`, `polygonWkt`. |
| GET | `/api/projects/export` | The same filter and sort, streamed as CSV. No paging. |
| GET | `/api/projects/nearby` | Projects within `radiusKm` of `latitude`/`longitude`, nearest first. Optional `limit` (default 100). |
| GET | `/api/projects/code-available` | Whether `code` is well formed and still free. Optional `excludeProjectId` when editing. |
| GET | `/api/projects/{id}` | One project with its activities and calculated progress. 404 if absent. |
| POST | `/api/projects` | Creates the project, its activities and its map feature. 201 with `Location`. |
| PUT | `/api/projects/{id}` | Updates the project, reconciles activities, syncs the map feature. |
| DELETE | `/api/projects/{id}` | Deletes the project, its activities and its map feature. 204. |
| POST | `/api/projects/bulk-delete` | Deletes the selected projects and reports each outcome. |

Sortable fields: `name`, `projectCode`, `projectTypeName`, `status`, `startDate`, `endDate`,
`activityCount`, `durationDays`, `lastModifiedOn`, `lastModifiedByName`. Anything else falls back
to `lastModifiedOn`. `dir` is descending unless it is exactly `asc`.

### Lookups

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/lookups/project-statuses` | `ProjectStatus` coded-value domain. |
| GET | `/api/lookups/activity-statuses` | `ActivityStatus` coded-value domain. |
| GET | `/api/lookups/project-types` | Project types from the database. |
| GET | `/api/lookups/users` | Users, for Project Owner and Assigned To. |

### GIS

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/gis/reconciliation` | Features with no project row, and project rows whose `ObjectId` no longer exists in the layer. Scoped to `SOURCEID >= 5000000`. |
| POST | `/api/gis/backfill-features` | Creates layer points for project rows that have a location but no `ObjectId`. Body: `batchSize` (default 200), `maxProjects`. Safe to re-run. **Writes to the shared public layer.** |

## Demo walkthrough

Start the stack and open `http://localhost:4200`. Steps 1–9 are the UI; 10–12 are API-only and
are easiest from Scalar at `<api>/scalar`.

1. **List paging and sorting.** The left pane opens on 5,000 seeded projects, 25 per page,
   sorted by last modified descending. Click a column header to sort; click again to reverse.
   Page through with the pager. Watch the request in the network tab — paging and sorting are
   server-side.
2. **Debounced search.** Type a partial project name or code in the search box. Requests are
   debounced by 300 ms, so a fast typist produces one request, not one per keystroke.
3. **Filters.** Filter by project type, status and start-date range. The active-filter count
   updates, and "Clear" resets everything including the spatial filters.
4. **Extent filter.** Pan and zoom the map, then toggle "Only projects in current map extent".
   The list is re-queried with the map's bounding box (`minLon`…`maxLat`).
5. **Polygon filter.** Draw a polygon on the map. It is sent to the server as WGS84 well-known
   text in `polygonWkt` and evaluated by PostGIS. Clear it from the filter bar.
6. **Selection sync.** Click a row: the map zooms to that project's point and selects it. Click
   a point: the popup shows the Project Code and the matching row is selected in the list. (This
   direction needs features in the layer — see limitations.)
7. **Shareable URL.** Note the query string: `lat`, `lon`, `z` and `sel` track the map centre,
   zoom and selected project, written with `replaceUrl` so panning does not fill the back stack.
   Copy the URL, open it in a new tab, and the same view and selection are restored.
8. **Add and edit with validation.** "New project" opens the form in the detail pane. Try a
   malformed code — it must match `^[A-Z]{3}-[0-9]{4}$`. Try an existing code — uniqueness is
   checked against `/api/projects/code-available` on blur. Drag the pin outside the Egypt
   boundary envelope and the location is rejected. Add activities: statuses follow the allowed
   transitions, and dates must sit inside the project window. Leave the form dirty and navigate
   away to see the unsaved-changes guard; the draft also autosaves to local storage every 30 s.
   Saving creates the feature first, then the row with the returned `ObjectId`.
9. **Delete with typed confirmation.** Delete a project you created. The dialog requires the
   project code to be typed exactly, case-sensitively. The row disappears optimistically and the
   layer feature is deleted in the same operation. Selecting several rows and deleting them uses
   `POST /api/projects/bulk-delete`, with the number of rows as the typed challenge.
10. **CSV export.** Use the export action with filters applied. The file streams from
    `/api/projects/export` with the same filter and sort as the grid, and its filename comes back
    in `Content-Disposition`.
11. **Nearby (spatial query).** `GET /api/projects/nearby?latitude=30.0444&longitude=31.2357&radiusKm=25`.
    Returns seeded projects around Cairo, nearest first, computed in PostGIS. API-only.
12. **Reconciliation.** `GET /api/gis/reconciliation`. Lists features under `SOURCEID >= 5000000`
    with no matching project row, and project rows whose `ObjectId` is no longer in the layer. On a
    clean run both lists are empty. To see it do work, create a project through the UI, then delete
    its row directly in the database and re-run the report — the feature is reported as an orphan.

To see the map populated with all 5,000 seeded projects, run
`POST /api/gis/backfill-features` first. Read the note below before you do.

## Known limitations

- **Seeded projects have no map features.** The seeder deliberately leaves `ObjectId` null on
  all 5,000 rows; writing 5,000 points into a shared public ArcGIS layer on every fresh database
  is not acceptable. Until `POST /api/gis/backfill-features` is run, the map shows only projects
  created through the UI, and map-to-list selection has nothing to match for seeded rows.
  Projects created or edited in the UI do get features immediately. The backfill is idempotent —
  it skips rows that already have an `ObjectId` and adopts an existing feature whose `SOURCEID`
  and project code both match rather than duplicating it; a slot held by another consumer's
  feature is logged and left alone — but it does write to a layer other people share, so run
  it knowingly and consider `maxProjects` to cap it.
- **No automated tests.** The rubric has no line for testing and none were written. `vitest` is
  configured in the client but there are no spec files. Behaviour was verified by exercising the
  endpoints against the seeded database.
- **No authentication.** Out of scope. The acting user is the constant `CurrentUser.Id = 1`, so
  every `CreatedBy` and `LastModifiedBy` attributes to the first seeded user.
- **The two stores are not transactional.** No transaction can span ArcGIS and PostgreSQL.
  Consistency comes from ordering plus compensation: create writes the feature first and deletes
  it if the database write fails; update and delete open the database transaction first and write
  the feature before committing. If a compensating delete also fails, an orphan feature remains —
  logged at critical, and surfaced by the reconciliation report.
- **The bulk backfill does not roll back a batch.** It uses `rollbackOnFailure=false` and writes
  back only the `ObjectId`s that succeeded. Failed rows keep no `ObjectId` and are retried on the
  next run.
- **The layer is used anonymously.** No credentials are configured. The 498/499
  refresh-and-retry-once path is implemented but is not exercised against this layer.
- **Migration fallback password.** `DesignTimeDbContextFactory` falls back to `Password=postgres`.
  If your `postgres-password` secret differs, EF tooling fails until you set
  `PMGIS_MIGRATIONS_CONNECTION`.
- **`APP_CONFIG` is compile-time.** Changing the layer URL or `sourceIdBase` for the client
  requires a rebuild, and both must be kept in step with the server's `ArcGis` section by hand.
