# Assumptions and Design Decisions

Decisions taken where the brief is silent, ambiguous, or leaves a genuine choice open.
Each entry records what the brief says, what was decided, and why — so a reviewer can
tell a considered choice from an oversight.

This file grows as the project does. It currently covers the domain and data-access
layers.

_Last updated: 2026-09-03_

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

### The PostGIS extension is declared in the model

**Decision:** The DbContext declares `postgis` as a required extension rather than
assuming a database that already has it enabled.

**Reasoning:** Declaring it in the model means the generated migration emits the statement
that installs it, so a fresh database is provisioned entirely by running migrations. The
alternative would be a manual step in the setup instructions that is easy to omit and
fails at query time rather than at deployment time.

---

## Out of scope so far

Recorded here so their absence reads as a decision rather than an omission.

- **Authentication.** The brief describes no sign-in and no roles, but requires audit
  fields (Last Modified By, and the user retained on a soft-deleted activity). Those
  fields exist on the entities; how the acting user is supplied is deferred to the API
  layer.
- **The allowed project boundary.** The brief requires a placed point to be validated
  against an allowed boundary but never defines one. To be decided when location handling
  is implemented.
- **Layer attribute schema.** Required as a deliverable. To be documented once the feature
  layer integration is built.
