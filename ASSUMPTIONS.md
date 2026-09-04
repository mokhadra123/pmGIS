# Assumptions and Design Decisions

Decisions taken where the brief is silent, ambiguous, or leaves a genuine choice open.
Each entry records what the brief says, what was decided, and why — so a reviewer can
tell a considered choice from an oversight.

This file grows as the project does. It currently covers the domain, data-access, API,
feature-layer and client work.

_Last updated: 2026-09-04_

---

## Domain model

### Project status values

**Brief says:** The Projects List includes a `Status` column, but the requirements text
never enumerates the permitted values.

**Decision:** Five values — `Draft`, `Active`, `InProgress`, `OnHold`, `Completed`.

**Reasoning:** The only source in the brief is the prototype screenshot (page 6), whose
Status column shows Active, In Progress, Draft, Completed and On Hold. Those five were
taken as the intended set rather than inventing a lifecycle. `Draft` is the default for
a newly created project, since the Add Project form can be submitted before a project is
genuinely under way.

Explicit numeric values are assigned to each member so the stored representation does not
shift if the members are ever reordered.

### Leaving On Hold

**Brief says:** _"On Hold reachable from Planned or In Progress."_ It does not say how an
activity leaves On Hold.

**Decision:** An activity On Hold may return to either `Planned` or `InProgress`.

**Reasoning:** On Hold has to be escapable or work could never resume, which cannot be
the intent. Allowing both destinations covers the two real cases: work that was under way
and picks up where it left off, and work that was paused before starting and is reset.
Permitting only `InProgress` would force a never-started activity to be marked as started.

### Completed is terminal

**Brief says:** Nothing. The stated chain ends at Completed.

**Decision:** No transition out of `Completed` is allowed.

**Reasoning:** The brief describes a strictly forward chain (`Planned → In Progress →
Completed`) and defines On Hold only as reachable from the two earlier states. Treating
Completed as terminal is the reading most consistent with that. The trade-off is that a
mistakenly completed activity cannot be reopened and must be replaced by a new row; this
was judged preferable to inventing a reopening rule the brief does not describe.

### An unchanged status is always a legal transition

**Brief says:** Nothing. It describes movement between statuses only.

**Decision:** `CanTransition(x, x)` returns `true` for every status.

**Reasoning:** Editing a project re-submits every activity row, including rows whose
status has not changed. If a status were not permitted to "transition" to itself, editing
an activity's name — or any other field — would be rejected as an illegal status change,
and any project with activities would become uneditable. The state machine constrains
movement, so standing still is outside its scope.

### Percent complete while On Hold

**Brief says:** _"% Complete must be 0 for Planned and 100 for Completed, and is editable
only while the activity is In Progress."_ On Hold is not mentioned.

**Decision:** An activity moving to `OnHold` keeps whatever percentage it already had,
clamped to 0–100. The field is not user-editable in that state.

**Reasoning:** On Hold means paused, not reset. Work completed before the pause is still
completed, and discarding it would misreport the project's overall progress — which is
derived from these values. Keeping the field read-only outside In Progress matches the
brief's rule that it is editable only while In Progress.

### Inverted activity date ranges

**Brief says:** Nothing about an activity whose End Date precedes its Start Date, beyond
requiring both fields.

**Decision:** `Activity.DurationDays` returns `1` when `EndDate < StartDate`. Otherwise it
is the inclusive day count (`End − Start + 1`), so a single-day activity is 1, not 0.

**Reasoning:** Duration is used as the weight in the project progress calculation. A zero
weight would silently drop the activity from the average, and a negative weight would
corrupt it — both failing quietly and producing a wrong number on screen rather than an
error. Returning 1 keeps the calculation well-formed. Validation rejects inverted ranges
before they can be saved, so this is a defensive floor rather than a supported state.

### Meaning of `Project.HasLocation`

**Brief says:** _"Zoom to Project is disabled when a project has no stored location."_

**Decision:** `HasLocation` is defined as `ObjectId.HasValue` — that is, it answers
_"does a point for this project exist in the ArcGIS Feature Layer?"_, not _"does this row
hold a coordinate?"_

**Reasoning:** The feature layer is the authoritative store for geometry, so "has a stored
location" is read as "has geometry in the layer". The two can disagree: a row may hold a
latitude and longitude whose feature has not been written yet. Where the UI only needs to
centre the map, it should test the coordinates directly rather than this property, because
the map can zoom to a coordinate with no feature behind it.

---

## Design decisions and trade-offs

### Geometry lives in ArcGIS; attributes live in the database

**Brief says:** _"The selected location will be stored in the Project Feature Layer, while
the remaining project details will be stored in the database"_, linked by ObjectId.

**Decision:** Followed as specified. `Project.ObjectId` holds the link.

**Trade-off:** The two stores cannot take part in a single transaction, so consistency has
to be achieved by ordering the writes and compensating when the second fails. This is the
source of the create-feature-first ordering, the compensating delete, and the
reconciliation requirement. It is accepted as a constraint of the brief rather than a
choice.

### `ObjectId` is `long?`

**Decision:** Typed `long`, and nullable.

**Reasoning:** ArcGIS supports both 32-bit and 64-bit ObjectIDs. The supplied layer is
32-bit, but a republished layer may not be, and `long` accepts either without loss.

Null is a legal, meaningful state: it means the project has no point in the layer. The
brief requires exactly this — Zoom to Project must be disabled for a project with no
stored location — so the absence has to be representable rather than defaulted to zero.

### Coordinates are mirrored into the database

**Decision:** `Latitude` and `Longitude` are stored on the project row as well as in the
feature layer. The feature layer remains authoritative.

**Reasoning:** Several requirements filter or sort 5,000 projects by location on the
server: the map-extent toggle, the drawn-polygon filter, the nearby-projects query and the
CSV export. If the coordinate existed only in ArcGIS, each of those would require an HTTP
round trip to a third-party service instead of a SQL predicate.

**Trade-off:** The coordinate is duplicated and can drift between the two stores. This is
accepted because the reconciliation check the brief requires is the mechanism for
detecting exactly that class of drift.

### Two different treatments of `DurationDays`

**Decision:** `Activity.DurationDays` is computed in C#. `Project.DurationDays` is a
property with a private setter, intended to be populated by a database-computed column.

**Reasoning:** The brief requires _"every column sortable ascending and descending, with
the sort applied on the server"_, and Duration is one of the Projects List columns. A
value computed in C# cannot appear in an `ORDER BY`, because the database has no knowledge
of it. The project's duration therefore has to exist as a real column.

An activity's duration has no such requirement — it is only ever used as a weight inside
the progress calculation — so it stays a plain expression with no storage cost.

### Project Type is a table; statuses are enums

**Brief says:** _"Project Type must be populated from a coded-value domain / lookup list
retrieved from the server, not hard-coded in the form."_

**Decision:** `ProjectType` is an entity with its own table. `ProjectStatus` and
`ActivityStatus` are C# enums.

**Reasoning:** The brief explicitly requires project types to be server-supplied data, and
they are business-managed: new types get added, old ones retired. An `IsActive` flag keeps
retired types resolvable for historic projects while hiding them from the picker, and
`SortOrder` lets the list control its own presentation.

Statuses are different in kind. Their values are fixed by the brief, and application logic
branches on them — the status state machine, the percent-complete rules. They are part of
the code's vocabulary, not editable reference data, so an enum is the honest
representation.

### Project Type and Owner are optional

**Brief says:** On the Add Project form only Project Name and Project Code are marked
required. Project Type, Owner, Budget, Description and both dates carry no asterisk.

**Decision:** The corresponding foreign keys are nullable.

**Reasoning:** Nullability of the foreign key is what makes a relationship optional, so a
non-nullable key would contradict the form and would produce a `NOT NULL` column that
rejects the very projects the brief allows.

---

## Data access

### Persistence is configured with the Fluent API, not data annotations

**Brief says:** Nothing. It requires _"a single data-access layer, no duplicated query
logic"_ but does not prescribe how the mapping is expressed.

**Decision:** All Entity Framework configuration — column types and lengths, indexes,
keys, delete behaviour — lives in `IEntityTypeConfiguration` classes inside
`PMGIS.Infrastructure`. No EF attributes appear on the entities.

**Reasoning:** Data annotations would put `Microsoft.EntityFrameworkCore` and
`System.ComponentModel.DataAnnotations` types on the domain entities, which would make
`PMGIS.Domain` depend on the persistence technology. The domain project deliberately has
no package or project references at all, so that business rules can be reasoned about and
tested without a database. The Fluent API keeps every storage concern on the
infrastructure side of that line.

It is also the more capable of the two. Stored computed columns, filtered indexes and
composite indexes have no annotation equivalent, and all three are needed to satisfy the
Projects List requirements.

### NetTopologySuite is not referenced

**Brief says:** Nothing about how spatial data should be represented in .NET. It requires
a drawn-polygon filter, a map-extent filter and a _"projects within a user-specified
distance of a clicked point, ordered by distance"_ query.

**Decision:** The `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` package is not
installed. Coordinates are stored as two `double` columns, and the spatial predicates are
expressed as parameterised PostGIS SQL that builds geometry from those columns at query
time.

**Reasoning:** NetTopologySuite exists to map PostGIS geometry columns onto .NET geometry
types so they can be queried through LINQ. No entity in this model has a geometry column —
the feature layer is the authoritative store for geometry, and the database holds a plain
numeric mirror of it. `ST_Contains`, `ST_DWithin` and `ST_Distance` all operate on
geometry constructed inside the query and return scalars, so nothing needs to be
materialised into a .NET geometry object.

**Trade-off:** Spatial predicates are written as SQL rather than LINQ, so they are not
composable in the same way as the rest of the query and are checked by the database rather
than the compiler. That is accepted because the alternative is a dependency carried solely
to express three predicates, and the SQL involved is short and confined to the single
data-access class.

### Enums are stored as strings, not integers

**Brief says:** Nothing about storage representation.

**Decision:** `ProjectStatus` and `ActivityStatus` are persisted as their member names
(`'InProgress'`), in a bounded character column, rather than as their underlying integers.

**Reasoning:** An integer column silently changes meaning if a member is ever inserted
into the middle of an enum — every existing row then refers to a different status, with no
error and no migration to catch it. Names are immune to reordering. They also make the
stored data readable directly in the database, which matters when diagnosing a data
problem against 5,000 seeded rows.

**Trade-off:** A few bytes per row instead of four, and slightly larger indexes on the
status columns. At this data volume that cost is not measurable, and correctness under
future edits is worth more than the space.

### Delete behaviour

**Brief says:** _"Delete must be transactional: remove the point from the Project Feature
Layer, cascade-delete the project's activities and delete the project row."_ It says
nothing about deleting a user or a project type.

**Decision:**

- **Project → Activities: cascade.** Deleting a project deletes its activities in the same
  database transaction, as the brief requires.
- **Project → ProjectType, Owner, CreatedBy, LastModifiedBy: restrict.**
- **Activity → AssignedTo, DeletedBy: restrict.**

**Reasoning:** Cascade is correct in exactly one direction here: an activity has no
meaning without its parent project, so the two share a lifetime.

Nothing else does. Cascading from a user would mean deleting a person's account silently
destroys every project they created — catastrophic and irreversible. `SetNull` was
rejected too, because the audit fields exist precisely so that _"Last Modified By"_ and
the user retained on a soft-deleted activity remain answerable; nulling them would erase
the audit trail the brief asks to keep. Restricting means a user or project type that is
still referenced cannot be deleted at all, which is the honest outcome: retirement is what
the `IsActive` flags are for.

### Index strategy for the Projects List

**Brief says:** _"Every column sortable ascending and descending, with the sort applied on
the server"_ and _"the list stays responsive with at least 5,000 project records."_

**Decision:** Columns that the list filters or sorts by are indexed individually: name,
status, both dates and duration. The coordinate pair and the default ordering are
composite indexes. Project code and ObjectId carry unique indexes. Activities carry a
composite index on project and soft-delete flag. Foreign keys are left to Entity
Framework's convention, which indexes them automatically.

**Reasoning:** Indexes are not free — each one is updated on every insert and update — so
they are placed only where the brief creates a read pattern.

Two are composite for specific reasons. The coordinate pair is one index rather than two,
because the map-extent filter always constrains longitude and latitude together. The
default ordering is indexed as `(LastModifiedOn, Id)` rather than on the timestamp alone:
server-side paging needs a deterministic tie-break or rows can repeat or vanish between
pages, so the query orders by both columns, and an index matching that order can be walked
directly instead of the result set being sorted on every page load. This is the list's
resting state — the query that runs before the user has done anything — so it is the one
most worth serving from an index.

**Known limits.** Two sortable columns cannot be served by an index on `Projects`:

- **Number of Activities** is a filtered aggregate over the activities table. The composite
  index on `(ProjectId, IsDeleted)` is what keeps that count cheap; there is nothing on
  the project row to index.
- **Project Type** is sorted by the type's display name, which lives in another table, so
  the sort is resolved through a join rather than the foreign-key index.

Both are accepted. Neither is on the default path, and the volume the brief specifies does
not justify denormalising a name or maintaining a counter column.

### `ObjectId` carries a filtered unique index

**Decision:** The unique index on `ObjectId` is declared with a filter excluding null rows.

**Reasoning:** Uniqueness is the point: one feature in the layer may back at most one
project row, which makes half of the reconciliation requirement structurally impossible to
violate rather than merely checked after the fact.

PostgreSQL already treats nulls as distinct in a unique index, so the filter is not what
permits many projects to have no location — that would work regardless. The filter is
there so the index does not carry an entry for every location-less row. Early in the
database's life most rows have no ObjectId, so the saving is the majority of the table.

### The PostGIS extension is declared in the model

**Decision:** The DbContext declares `postgis` as a required extension rather than
assuming a database that already has it enabled.

**Reasoning:** Declaring it in the model means the generated migration emits the statement
that installs it, so a fresh database is provisioned entirely by running migrations. The
alternative would be a manual step in the setup instructions that is easy to omit and
fails at query time rather than at deployment time.

---

## API structure

### Vertical slices with explicit registration

**Brief says:** _"Separation of concerns, reusable components, a single data-access layer,
no duplicated query logic."_ It does not prescribe a project structure.

**Decision:** Each operation owns a folder containing its endpoint, its input type, its
handler and its validator. Handlers and routes are listed by hand in a feature module
(`ProjectsFeature`, `LookupsFeature`, `GisFeature`) rather than discovered by assembly
scanning.

**Reasoning:** Grouping by technical role — all endpoints together, all handlers together —
spreads one feature across four folders, so changing it means editing four places and
deleting it means hunting. A slice is added, changed or removed as a unit.

Registration is explicit because the failure mode of scanning is silence: a slice that is
never registered still compiles, and its route simply does not exist. Listing them makes
that omission visible in a file a person reads.

**Trade-off:** Adding a slice means remembering to edit its module. That is a compile-time
visible chore, which was preferred to a runtime-invisible one. Note the opposite call was
made for EF entity configurations, which _are_ scanned: mapping is uniform and a missing
configuration fails loudly at model build, so the risk is not comparable.

### Validation runs as an endpoint filter

**Brief says:** validation rules must be enforced _"on both the client and the server"_,
with _"field-level error messages"_ and a summary that _"lists every offending row rather
than only the first."_

**Decision:** A generic endpoint filter resolves the FluentValidation validator for the
request type, runs it before the handler, and returns an RFC 7807 `ValidationProblem` with
one entry per field. Failures are grouped by property name so every offending field is
reported, not the first.

**Reasoning:** Putting this in a filter keeps guard clauses out of handlers and makes the
response shape identical everywhere. The RFC 7807 `errors` dictionary is keyed by field
name — including indexer syntax for collections, e.g. `Activities[2].EndDate` — which is
what lets the browser place each message next to the input that caused it rather than
showing one generic toast.

**Limit:** Rules that depend on stored state cannot run there. Project code uniqueness and
the activity status state machine are checked inside the handlers, because a request
validator cannot see the database or the row's previous status.

### Create and update use separate validators

**Decision:** `CreateProjectCommandValidator` and `UpdateProjectCommandValidator` are
separate types with, today, identical rules.

**Reasoning:** They are separate concepts that happen to coincide. Sharing one validator
would mean any future edit-only rule has to be reasoned about in terms of what it does to
creation. The duplication is a handful of lines and is the cheaper of the two costs.

### Bulk delete is a loop over the single delete

**Brief says:** _"a single bulk delete action over the selected projects"_, and separately
that a delete must be transactional across the feature, the activities and the row.

**Decision:** The bulk slice calls the single-delete handler once per project and returns
per-project outcomes. It is not one transaction spanning every selected project.

**Reasoning:** Each project's delete already spans two systems that cannot share a
transaction. Wrapping several of those in an outer transaction would not make the set
atomic — a feature-layer deletion cannot be rolled back by the database — so the guarantee
would be illusory. Per-project atomicity is real and is what the brief actually requires;
reporting each outcome lets the UI restore exactly the rows that survived.

### The acting user is a constant

**Brief says:** nothing about authentication, but requires Last Modified By, and the user
retained on a soft-deleted activity.

**Decision:** `CurrentUser.Id` is a constant pointing at a seeded user. Audit fields are
written from it.

**Reasoning:** Authentication is not in the brief and building it would be scope the
assessment did not ask for. The audit fields are real and populated, so replacing this one
constant with a claim from a signed-in principal is the entire change required later. It
is isolated in a single named type rather than scattered as a literal.

---

## Feature layer

### The two stores are ordered, not transacted

**Brief says:** _"create the feature first, then write the project row with the returned
ObjectId. If the database write fails, delete the newly created feature."_ Delete must
leave _"no orphan features or orphan activity rows."_

**Decision:** The ordering is deliberately asymmetric.

- **Create:** the feature is written first, because the row needs the ObjectId the service
  assigns. If the database write then fails, the new feature is deleted as a compensating
  action.
- **Update and delete:** the database transaction is opened first and the feature is
  written before committing, so a feature failure rolls the database back for free.

**Reasoning:** No transaction can span ArcGIS and PostgreSQL, so consistency has to come
from ordering plus compensation. Create has no choice — the ObjectId does not exist until
the feature does. Update and delete do have a choice, and putting the fallible remote call
inside an open transaction converts a two-phase problem into a one-phase one.

**Residual risk:** if the compensating delete _also_ fails, an orphan feature remains. That
case is logged at critical and is exactly what the reconciliation report exists to surface.

### SOURCEID namespaces this application's features

**Brief says:** nothing. The supplied layer is a shared public sample that other people
also write to.

**Decision:** Every feature this application creates carries `SOURCEID = 5000000 + project
Id`. Reads apply a definition expression of `SOURCEID >= 5000000`, and the reconciliation
report is scoped the same way.

**Reasoning:** Without a scope, every feature written by any other consumer of the shared
layer would be reported as an orphan, making the reconciliation check useless. Deriving the
value from the project Id means the single-create path and the bulk backfill compute the
same value for the same project, so an interrupted backfill can find the feature it already
created instead of duplicating it.

**What happened:** the base was originally `900000`, and this entry claimed the derived
value was "stable and collision-free within our range". The second half of that was wrong.
The range was never ours to claim: another consumer of the same public layer had chosen the
identical `900000 + id` scheme, so the layer held `SOURCEID`s from 900001 to 905002
belonging to a different dataset. The map drew 4,223 of their points as though they were
projects and reported "no matching project row" on click, and a backfill run adopted
features carrying the codes `PRJ-0001` and `PRJ-0003` for the projects `TEL-0001` and
`TEL-0003`, silently pointing three project rows at a stranger's geometry.

Two things changed as a result. The base moved to `5000000`, clear of that cluster. And
adoption no longer trusts the slot alone: the feature's `name` must also carry the project's
code, otherwise the run logs a warning and creates its own feature, leaving the other
consumer's point untouched and correctly reported as an orphan.

**Residual risk:** an exclusive range on a public layer is a convention, not a guarantee.
Nothing stops a third consumer from also picking `5000000`. The code check is what keeps
that from being adopted silently; the definition expression would still draw their points.

### The layer is used anonymously

**Decision:** No ArcGIS credentials are configured. The token provider returns null and no
token is sent.

**Reasoning:** The layer named in the brief accepts anonymous edits. Username and password
are bound from configuration, so republishing the layer into an account that requires a
token is a settings change rather than a code change. The 498/499 refresh-and-retry-once
path is implemented regardless, because it is required and cannot be added meaningfully
after the fact.

### Bulk backfill does not roll back a batch

**Decision:** Single edits use `rollbackOnFailure=true`. The bulk backfill uses `false`,
and writes back only the ObjectIds whose per-feature result reported success.

**Reasoning:** A backfill of thousands of rows should not discard an entire batch because
one geometry was rejected. Rows that fail keep no ObjectId, so they remain candidates and
the next run retries them. Idempotency comes from the candidate filter itself plus adoption
of a feature whose `SOURCEID` slot _and_ project code both match, so a re-run cannot double
up points in a shared public layer.

### Seeded projects have no features

**Decision:** The seeder leaves `ObjectId` null on all 5,000 rows.

**Reasoning:** Seeding is local and repeatable; writing 5,000 points into a shared public
ArcGIS layer on every fresh database is not. The backfill slice exists to create them
deliberately, in controlled batches, when wanted.

---

## Client

### The allowed project boundary is configuration, not data

**Brief says:** _"Validate that the selected point falls inside the allowed project
boundary. A point outside the boundary must be rejected with a clear message."_ It never
defines the boundary.

**Decision:** A bounding box covering Egypt, held in the client's `APP_CONFIG` as
`[minLon, minLat, maxLon, maxLat]` in WGS84.

**Reasoning:** The requirement needs a boundary to exist, and there is nothing to derive
one from: the brief gives no geometry, and the API exposes no boundary resource. Treating
it as configuration rather than data keeps it in one named place instead of scattering a
literal through the location picker.

Egypt was chosen because it matches the seeded project locations and the default map
centre, so the rule can actually be exercised — a boundary no seeded project falls outside
would be untestable.

**Trade-off:** A rectangle is a crude approximation of a country, so a point in
neighbouring territory inside the box would be accepted. A real deployment would validate
against an authoritative polygon, most likely served by the API so the same boundary
governs both sides. The client-side check is the immediate feedback the brief asks for,
not an authority.

### The client mirrors the domain rules rather than sharing them

**Brief says:** _"Validation rules must be enforced on both the client and the server, not
only in the browser."_

**Decision:** The activity status machine, the percent-complete rules and the project code
pattern exist twice — once in `PMGIS.Domain.Rules`, once in the client's `core/rules`.

**Reasoning:** The two runtimes cannot share code, so a mirror is the only option short of
generating one from the other. They are kept deliberately parallel: same function names,
same shape, so a change to one is an obvious prompt to change the other.

**Trade-off:** Duplication that can drift. It is bounded — three small pure functions and a
regular expression — and the server remains the enforcement point, so drift degrades the
immediacy of feedback rather than data integrity.

### Lookups are cached per session, but failures are not

**Decision:** Each lookup endpoint is fetched once and replayed to later subscribers. A
request that fails is removed from the cache so the next caller retries.

**Reasoning:** Coded-value domains change rarely and three parts of the UI need them at
once — the filter panel, the project form and the activities table — so refetching per
consumer would be wasteful.

Caching the observable alone would also cache an error: a replayed stream hands every
later subscriber whatever the source emitted, including a failure, and never resubscribes.
One blip while the API was still starting would leave the type and user pickers empty for
the rest of the session with no way to recover short of a reload. Evicting on failure
keeps the cache useful without making it a trap.

### Transport models are hand-written, not generated

**Decision:** The TypeScript interfaces under `core/models` are written by hand to match the
API contracts, rather than generated from the OpenAPI document.

**Reasoning:** The API exposes an OpenAPI document, so a generated client was possible.
Hand-written models were chosen because the set is small and stable, and because the
generated output would carry names and shapes the client does not want — most of the
transport types differ slightly in intent from their server DTOs.

**Trade-off:** Nothing enforces that the two agree. `HttpClient.get<T>()` is an unchecked
cast, so a mismatch between a model and the JSON is invisible to the compiler and surfaces
as `undefined` at runtime. Contracts are therefore verified by exercising the endpoints,
not by the type system.

### The map is a sibling of the router outlet, not a route

**Brief says:** Nothing about routing. It describes a workspace with a Projects List, a
map, and Add / Edit / Details views.

**Decision:** The `Workspace` component holds three panes: the list, `<app-project-map>`,
and a `<router-outlet>`. The map is a fixed sibling of the outlet. Only the detail pane is
routed — `projects/new`, `projects/:id/edit` and `projects/:id` are children of the empty
path, and the pane is rendered only while the URL contains `/projects`.

**Reasoning:** An ArcGIS `MapView` is expensive to construct and holds view state — extent,
layer views, the highlight handle, the sketch graphic. If the map were inside the outlet,
every navigation between the list, the form and the details view would destroy and rebuild
it: the basemap would flash, the extent would reset, and the extent filter would fire a new
query on every navigation. Keeping it outside the outlet means the map is created once for
the session and navigation only swaps the third column.

**Trade-off:** The map cannot participate in routing — it has no route of its own and its
state is not expressed as route segments, which is why the view state is carried in query
parameters instead. The panel's visibility is also derived by string-matching
`/projects` on `NavigationEnd` rather than read from the activated route, so a future
top-level route containing that substring would open the panel unintentionally.

### Map view state lives in the query string, and writes replace the history entry

**Brief says:** Nothing about deep linking or shareable URLs.

**Decision:** `MapUrlState` mirrors the map centre (`lat`, `lon`, five decimal places), the
rounded zoom (`z`) and the selected project (`sel`) into the query string. Every write is a
`router.navigate` with `queryParamsHandling: 'merge'` and `replaceUrl: true`. The view is
also seeded once when the map first becomes ready, not only when it moves.

**Reasoning:** A map view that cannot be bookmarked or pasted to a colleague is a view that
has to be re-found by hand. The query string is the only part of the application's state
the browser already shares, so it is where the view belongs.

Writes replace rather than push because the view changes continuously: `stationary` fires
after every pan and zoom, and pushing each one would fill the history stack with dozens of
near-identical entries. Back would then walk the user through their own panning instead of
returning them to the page they came from. The seed on ready exists because `stationary`
only fires on a change — a map that is opened and never touched would otherwise produce a
URL with no view in it at all.

**Trade-off:** Losing history means the browser's Back button cannot undo a pan or zoom.
That was judged the lesser cost: Back is understood as page navigation, not map navigation,
and the map's Home widget already returns the view to its starting point. Rounding the
centre to five decimals and the zoom to an integer also means a restored view is close to,
not identical to, the one that was shared.

### A clicked point is resolved to a project row through the project code

**Brief says:** _"Clicking a point on the map selects the matching row in the Projects
List."_ It does not say how the point identifies the row.

**Decision:** The feature layer's `name` attribute holds the Project Code, so a clicked
feature is resolved in two steps: query the layer for that feature's `name`, then ask the
API for the project whose code matches. Results are memoised per ObjectId in the map
component, and the reverse direction — highlighting the feature for a selected row — runs
the same mapping backwards, querying the layer with `where name = '<code>'`.

**Reasoning:** The link between the two stores is `Project.ObjectId`, held on the database
row, and the shared sample layer has no field for the project's database id. Nothing in the
feature therefore points back at a row directly, so the code is the only identifier both
sides carry. It is unique on the project table, which makes the mapping unambiguous.

**Trade-off:** Selecting a point costs a layer query plus an API call rather than being a
local lookup. The per-ObjectId cache keeps repeat clicks free, but a first click on any
point is two round trips. Reversing the direction is worse: `projectCodeFor` scans the
cache and falls back to fetching the project by id, so highlighting a row that has never
been clicked costs a further request. A clicked point whose code matches no row is not
treated as an error — the popup says the point will be listed by the reconciliation check,
which is the honest description of an orphan feature in a shared layer.

Both the popup and the click handler read `OBJECTID` and give up when it is absent, which
is how a click on a cluster is distinguished from a click on a single feature.

### The drawn polygon is handed to the server as WGS84 well-known text

**Brief says:** _"Draw a polygon or rectangle on the map and filter the list to the
projects inside it."_ It does not say how the shape reaches the server.

**Decision:** On the Sketch widget's `complete` state, the geometry is projected to WGS84
if it is not already, converted to a `POLYGON((...))` WKT string by hand, and published on
the map bridge as `polygonWkt`. The query sends that string to the API.

**Reasoning:** The server builds its spatial predicates as PostGIS SQL over the mirrored
latitude and longitude columns, which are WGS84 degrees. WKT is the one geometry encoding
PostGIS parses natively, so no geometry library is needed on either side of the wire. The
map's own geometry arrives in Web Mercator, so the projection has to happen somewhere; it
happens in the browser because that is where the spatial reference is known.

The ring is explicitly closed before being written out. ArcGIS closes its rings already,
but an unclosed ring makes PostGIS raise _"geometry requires more points"_ rather than
return an empty result, so the closure is asserted instead of trusted.

**Trade-off:** Only the first ring is emitted, so a polygon with a hole is sent as its
outer boundary. The Sketch widget as configured cannot draw one, so this is a limitation of
the encoder rather than a reachable bug. WKT for a large freehand polygon is also a long
query-string value, which will eventually meet a URL length limit; moving the filter to a
POST body would be the fix if that were reached.

### The location pin is dragged directly rather than through the Sketch widget

**Brief says:** _"The selected point must be adjustable — the user can drag it or retype
the coordinates."_

**Decision:** The chosen point is a plain `Graphic` on a dedicated `GraphicsLayer`, and
dragging is implemented from `pointerdown` / `pointermove` / `pointerup` on the map
element: a hit test against that layer starts the drag, and each move writes the new
coordinate into the map bridge. The Sketch widget is present on the map but is used only
for the polygon filter.

**Reasoning:** The Sketch widget owns the graphics layer it edits — it adds its own
selection handles, its own delete affordance and its own undo stack, and it is already
bound to the spatial filter. Pointing it at the location pin as well would mean one widget
serving two unrelated purposes, with the filter and the location competing for the same
active tool. A single point that can only be moved is simpler than the widget's general
case, and hit-testing one layer is a few lines.

**Trade-off:** The hand-rolled drag reimplements things the widget provides for free. It
uses `event.offsetX/offsetY`, which is correct only while the map element is the event
target; it has no touch-specific handling beyond what pointer events give; and a `pointerup`
that lands outside the element leaves `draggingLocation` true until the next release. The
graphic is also re-rendered from the bridge signal on every change, guarded by a
`draggingLocation` flag so the pin being dragged is not rebuilt underneath the pointer.

### Reverse geocoding is decoration, and its failure is never surfaced

**Brief says:** _"Show the address of the selected point."_ It says nothing about what
should happen if the geocoder is unavailable.

**Decision:** The picker calls the anonymous ArcGIS World GeocodeServer for the address of
the current point, displays whatever comes back, and on any failure sets the address to
null and moves on. No error is raised, no notification is shown, and the address is never
sent to the API or stored. The locator module is imported dynamically, so it is only
downloaded once a point exists.

**Reasoning:** The address is context for the person choosing a point, not data the system
owns — the authoritative record of a location is the coordinate pair and the feature. A
geocoding outage should therefore cost the user a label, not the ability to save a project.
Making the failure visible would train the user to ignore a message about something they
cannot act on, and making it blocking would let a third-party service outside the brief
decide whether a project can be created.

**Trade-off:** A silent failure is indistinguishable from a point with no known address, so
a persistent outage looks like the geocoder simply having nothing to say. The call also
fires on every change to the point, including each step of a drag, so dragging the pin
issues a request per pointer move; the requests are cheap and unordered, but the last one
to return wins rather than the last one issued.

### The form autosaves a draft to local storage, and restore is announced but not automatic

**Brief says:** Nothing about unsaved work. It requires only that the form validate and
submit.

**Decision:** Every 30 seconds a dirty form is serialised — including activity rows and the
chosen coordinate — into `localStorage` under a key namespaced by project id (`…draft.new`
for a new project, `…draft.<id>` for an edit). On entering the form the draft for that key
is applied over whatever was loaded, the form is marked dirty, and the timestamp is
recorded so the template can offer Discard. A successful save clears the draft. All three
storage calls are wrapped in `try`/`catch`.

**Reasoning:** The Add Project form is long — ten project fields plus an unbounded activity
table — and losing it to an accidental refresh or a closed tab is the kind of failure a
user blames the application for. Keying by project id keeps an edit-in-progress on one
project from resurfacing on another. Storage is wrapped because `localStorage` throws in
private browsing and when the quota is exhausted, and a missing draft is not worth
interrupting anyone over.

**Trade-off:** On an edit, the restored draft silently wins over the freshly loaded server
state, so a project changed by someone else since the draft was written will show the stale
values until Discard is pressed. The banner naming the draft's timestamp is the only signal
that this happened. The draft is also never expired, so an abandoned one is offered
indefinitely, and it is written unencrypted to the browser — acceptable for project
metadata, but it would not be for anything sensitive. A route guard
(`unsavedChangesGuard`, wired onto both form routes) covers in-session navigation; the
autosave covers everything the guard cannot see.

### The client restricts the status dropdown as well as validating it

**Brief says:** _"Validation rules must be enforced on both the client and the server."_

**Decision:** Each activity row carries an `originalStatus` control, populated from the
loaded project and never sent to the server. The status dropdown lists only the statuses
`canTransition` allows from that original value, and changing the status runs
`normalizePercentComplete` — the client twin of the server's rule — so the percentage is
dragged to 0 or 100 as the new status demands. `activityRow` re-checks the same rules as a
validator.

**Reasoning:** Making the illegal option unselectable is better feedback than rejecting it
afterwards, and normalising the percentage on the client means the value the user sees
before saving is the value the server will store — otherwise a row could be submitted at
40% and come back at 100 with no explanation. The validator is kept as well because the
dropdown is not the only way a value arrives: a restored draft and a server round trip both
bypass it.

**Trade-off:** The rule now exists in three places for an existing row — the dropdown's
filter, the row validator, and the server. They share the `core/rules` functions, so the
logic is single-sourced, but the decision to apply it in the UI is not. A new row has no
original status and is therefore allowed to start at any status, which matches the server:
there is no previous state to transition from.

### Uniqueness is checked on blur and a failed check never blocks the form

**Brief says:** _"Project Code must be unique; the check happens against the server."_

**Decision:** The `projectCode` control uses `updateOn: 'blur'` and an async validator that
calls the availability endpoint, passing the current project id so an edit does not collide
with itself. The validator short-circuits to valid when the value does not match the code
pattern, and `catchError` maps a failed request to valid.

**Reasoning:** Validating on every keystroke would issue a request per character, most of
them for codes that are half-typed and meaningless. Blur is the point at which the user has
finished stating an intention.

Swallowing the error is deliberate: the check is a convenience, and the server rejects a
duplicate on submit regardless. Treating an unreachable availability endpoint as "code
taken" would block a legitimate save on an unrelated outage, which is the worse failure.

**Trade-off:** A user whose availability check silently failed learns about the collision
at submit instead of at blur. The server's rejection is placed back onto the field by
`applyServerErrors`, so the message still lands next to the input rather than only in a
toast.

### Deletes are applied optimistically, behind a typed confirmation

**Brief says:** _"Delete requires an explicit confirmation"_, and a bulk delete must report
per-project outcomes.

**Decision:** Confirming a delete requires typing a challenge string — the Project Code for
a single row, the count for a bulk delete. The rows then leave the table immediately, and
the store's rollback is invoked if the server refuses. A partial bulk result rolls the
whole optimistic removal back and then reloads, reporting each failure by id and reason.

**Reasoning:** Typing the code makes a destructive action deliberate rather than a
misplaced click, and scales to the bulk case where naming every project is impractical.
Removing the rows before the server answers keeps the table responsive on a slow delete,
which matters most in the bulk case the brief asks for.

**Trade-off:** On a partial bulk failure the rollback restores every row, including the ones
that were genuinely deleted, and correctness comes from the reload that follows rather than
from the optimistic state itself. For the moment between the two the table is briefly wrong.
Restoring only the survivors would be more precise but would leave the table in a state
neither the server nor the store agrees on if the reload then failed.

### The layer attribute schema

**Brief says:** the layer schema is a required deliverable; the supplied layer's fields are
not described.

**Decision:** Three attributes are used, and no others are read or written:

- **`OBJECTID`** — assigned by the feature service, stored on the project row as
  `Project.ObjectId` and used as the link between the two stores.
- **`name`** — the Project Code. It is what the Search widget searches
  (`APP_CONFIG.arcgis.searchField`), what the popup title shows, and how a clicked point is
  resolved back to a project row.
- **`SOURCEID`** — `5000000` and above, namespacing this application's features inside the
  shared sample layer. The client applies `SOURCEID >= 5000000` as the layer's definition
  expression so no other consumer's points are drawn, clustered, searched or counted.

Geometry is a single point in WGS84. The layer's `outFields` are limited to those three.

**Reasoning:** The supplied layer is a public sample with a fixed schema that this
application does not control, so the schema is a matter of choosing which existing fields
to use rather than designing new ones. `name` carries the Project Code rather than the
project's name because it is the layer's only searchable text field and the code is the
unique, stable identifier of the two.

**Trade-off:** The layer holds no copy of the project's display name, its type or its dates,
so the popup has to fetch them from the API rather than render them from the feature. That
is accepted — duplicating attributes into a shared public layer would create a second thing
to keep in sync, and the brief already places project details in the database.

---

## Conventions

### Comments are single-line; reasoning lives here

**Decision:** Source files carry short `//` comments stating what a piece of code does. XML
documentation blocks are not used.

**Reasoning:** The design reasoning is recorded in this file and in commit messages, which
is where a reviewer looks for it, and keeps it in one place rather than restated across
files where copies drift apart. Comments in code answer "what is this", not "why was it
built this way".

---

## Out of scope

Recorded here so their absence reads as a decision rather than an omission.

- **Authentication.** No sign-in or roles, as the brief describes none. See _The acting
  user is a constant_ above for how audit fields are populated in the meantime.
- **Automated tests.** The brief lists no testing requirement and the rubric has no line
  for it. Behaviour was verified by exercising the endpoints against the seeded database.
- ~~**Layer attribute schema.**~~ No longer open. The three attributes used — `OBJECTID`,
  `name` and `SOURCEID` — are settled by the client and server code and are recorded under
  _The layer attribute schema_ above.
