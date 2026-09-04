import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import '@arcgis/map-components/components/arcgis-map';
import '@arcgis/map-components/components/arcgis-zoom';
import '@arcgis/map-components/components/arcgis-home';
import '@arcgis/map-components/components/arcgis-search';
import '@arcgis/map-components/components/arcgis-legend';
import '@arcgis/map-components/components/arcgis-layer-list';
import '@arcgis/map-components/components/arcgis-basemap-gallery';
import '@arcgis/map-components/components/arcgis-scale-bar';
import '@arcgis/map-components/components/arcgis-coordinate-conversion';
import '@arcgis/map-components/components/arcgis-expand';
import '@arcgis/map-components/components/arcgis-sketch';

import ArcgisMap from '@arcgis/core/Map';
import Graphic from '@arcgis/core/Graphic';
import GraphicsLayer from '@arcgis/core/layers/GraphicsLayer';
import FeatureLayer from '@arcgis/core/layers/FeatureLayer';
import Point from '@arcgis/core/geometry/Point';
import LayerSearchSource from '@arcgis/core/widgets/Search/LayerSearchSource';
import * as webMercatorUtils from '@arcgis/core/geometry/support/webMercatorUtils';
import type MapView from '@arcgis/core/views/MapView';
import type Extent from '@arcgis/core/geometry/Extent';
import type Polygon from '@arcgis/core/geometry/Polygon';

// Union of the CreateEvent and UpdateEvent payloads arcgis-sketch dispatches.
interface SketchEventDetail {
  state?: string;
  graphic?: Graphic | null;
  graphics?: Graphic[];
}

import { APP_CONFIG } from '@core/config/app-config';
import { ProjectsApi } from '@core/api/projects-api';
import type { ProjectListItem } from '@core/models/project';
import { MapBridge } from '@core/state/map-bridge';
import { Notifications } from '@shared/notifications/notifications';

import { MapUrlState } from './map-url-state';
import { PagedResult } from '@core/models/paging';

// What FeatureLayerView.highlight() hands back.
type RemovableHandle = { remove(): void };

// The bits of <arcgis-map> this component drives.
type ArcgisMapElement = HTMLElement & {
  viewOnReady(): Promise<void>;
  view: MapView;
};

// The subset of a clicked project point the popup and the selection actually use.
interface ProjectPointSummary {
  readonly id: number;
  readonly name: string;
  readonly projectCode: string;
  readonly projectTypeName: string | null;
  readonly startDate: string | null;
  readonly endDate: string | null;
  readonly activityCount: number;
}

// The map half of the application.
@Component({
  selector: 'app-project-map',
  standalone: true,
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-map.html',
  styleUrl: './project-map.scss',
})
export class ProjectMap {
  private readonly bridge = inject(MapBridge);
  private readonly urlState = inject(MapUrlState);
  private readonly api = inject(ProjectsApi);
  private readonly notifications = inject(Notifications);
  private readonly destroyRef = inject(DestroyRef);

  readonly center = [...APP_CONFIG.map.initialCenter] as [number, number];
  readonly zoom = APP_CONFIG.map.initialZoom;

  private readonly mapElement = viewChild.required<ElementRef<ArcgisMapElement>>('mapElement');

  private view: MapView | null = null;
  private highlightHandle: RemovableHandle | null = null;
  private locationGraphic: Graphic | null = null;
  private draggingLocation = false;

  // Holds the draggable "chosen location" pin.
  private readonly editLayer = new GraphicsLayer({
    title: 'Selected location',
    listMode: 'hide',
  });

  private readonly projectLayer = new FeatureLayer({
    url: APP_CONFIG.arcgis.projectLayerURL,
    title: 'Projects',
    // Only the attributes actually rendered, to keep query payloads small.
    outFields: ['OBJECTID', 'name', 'SOURCEID'],
    definitionExpression: `SOURCEID >= ${APP_CONFIG.arcgis.sourceIdBase}`,

    popupTemplate: {
      title: '{name}',
      // The popup needs project details the feature layer does not carry, so it is
      // filled in from the API keyed on the code held in `name`.
      content: async (event: { graphic: Graphic }) => {
        const node = document.createElement('div');
        node.className = 'project-popup';
        node.textContent = 'Loading project…';

        const objectId = event.graphic.getAttribute('OBJECTID') as number | null;
        const project = objectId === null ? null : await this.lookupByObjectId(objectId);

        if (!project) {
          node.textContent =
            'This point has no matching project row. It will be listed by the reconciliation check.';
          return node;
        }

        node.innerHTML = `
          <dl>
            <dt>Project Name</dt><dd>${escapeHtml(project.name)}</dd>
            <dt>Project Code</dt><dd>${escapeHtml(project.projectCode)}</dd>
            <dt>Type</dt><dd>${escapeHtml(project.projectTypeName ?? '—')}</dd>
            <dt>Start / End</dt><dd>${project.startDate ?? '—'} → ${project.endDate ?? '—'}</dd>
            <dt>Activities</dt><dd>${project.activityCount}</dd>
          </dl>`;
        return node;
      },
    },

    featureReduction: {
      type: 'cluster',

      maxScale: APP_CONFIG.map.clusterMaxScale,

      clusterRadius: '80px',
      clusterMinSize: '24px',
      clusterMaxSize: '56px',

      popupTemplate: {
        title: 'Project cluster',
        content: '{cluster_count} projects in this area.',
        fieldInfos: [
          {
            fieldName: 'cluster_count',
            format: { digitSeparator: true, places: 0 },
          },
        ],
      },

      labelingInfo: [
        {
          deconflictionStrategy: 'none',
          labelPlacement: 'center-center',
          labelExpressionInfo: {
            expression: "Text($feature.cluster_count, '#,###')",
          },
          symbol: {
            type: 'text',
            color: '#ffffff',
            haloColor: '#1b5583',
            haloSize: 1,
            font: {
              family: 'Noto Sans',
              size: 11,
              weight: 'bold',
            },
          },
        },
      ],
    },
  });

  readonly map = new ArcgisMap({
    basemap: 'streets-vector',
    layers: [this.projectLayer, this.editLayer],
  });

  constructor() {
    // A location chosen elsewhere (typed coordinates, or a project loaded for edit)
    // must appear on the map.
    effect(() => {
      const point = this.bridge.draftLocation();
      if (!this.draggingLocation) {
        this.renderLocationGraphic(point);
      }
    });

    // Selecting a row highlights the matching point.
    this.bridge.highlightRequested
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((projectId) => void this.highlight(projectId));

    // "Zoom to Project", and restoring a shared URL.
    this.bridge.gotoRequested.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((target) => {
      void this.view?.goTo({
        center: [target.longitude, target.latitude],
        zoom: target.zoom ?? APP_CONFIG.map.projectZoom,
      });
    });

    afterNextRender(() => void this.initialiseView());

    this.destroyRef.onDestroy(() => {
      this.highlightHandle?.remove();
      this.highlightHandle = null;
    });
  }

  // ----- view wiring ---------------------------------------------------------

  // viewOnReady() rather than the ready event, which can fire before Angular listens.
  private async initialiseView(): Promise<void> {
    const element = this.mapElement().nativeElement;
    await element.viewOnReady();

    const view = element.view;
    this.view = view;

    this.restoreFromUrl(view);
    this.publishExtent(view);
    // Seed the URL from the opening view. `stationary` only fires on a change, so
    // without this a map that is never touched would produce an unshareable URL.
    this.urlState.writeView(this.toLatLng(view.center), Math.round(view.zoom));

    // Extent feeds the "only projects in current map extent" toggle and the URL.
    view.watch('stationary', (stationary: boolean) => {
      if (stationary) {
        this.publishExtent(view);
        this.urlState.writeView(this.toLatLng(view.center), Math.round(view.zoom));
      }
    });

    view.on('click', (event) => void this.onMapClick(view, event));

    // Feature-layer failures must be visible and recoverable, never a stuck spinner.
    this.projectLayer.when(undefined, (error: unknown) => this.reportLayerFailure(error));
  }

  private publishExtent(view: MapView): void {
    const extent = view.extent;
    if (!extent) return;

    const geographic = webMercatorUtils.webMercatorToGeographic(extent) as Extent;
    this.bridge.extent.set([geographic.xmin, geographic.ymin, geographic.xmax, geographic.ymax]);
  }

  private toLatLng(point: Point): { latitude: number; longitude: number } {
    const geographic =
      point.spatialReference?.isWGS84 === true
        ? point
        : (webMercatorUtils.webMercatorToGeographic(point) as Point);

    return { latitude: geographic.latitude ?? 0, longitude: geographic.longitude ?? 0 };
  }

  private restoreFromUrl(view: MapView): void {
    const state = this.urlState.read();

    if (state.centre) {
      void view.goTo({
        center: [state.centre.longitude, state.centre.latitude],
        zoom: state.zoom ?? APP_CONFIG.map.projectZoom,
      });
    }

    if (state.selectedProjectId !== null) {
      this.bridge.selectFromList(state.selectedProjectId);
    }
  }

  // ----- clicking ------------------------------------------------------------

  private async onMapClick(
    view: MapView,
    event: { mapPoint: Point; stopPropagation(): void },
  ): Promise<void> {
    // While the form is picking a location, a click places the point and nothing else.
    if (this.bridge.pickingLocation()) {
      event.stopPropagation();
      const { latitude, longitude } = this.toLatLng(event.mapPoint);
      this.bridge.draftLocation.set({ latitude, longitude });
      this.bridge.stopPicking();
      return;
    }

    // Otherwise a click on a project point selects the matching row.
    try {
      const hit = await view.hitTest(event as never, { include: [this.projectLayer] });
      const graphic = hit.results
        .filter((result) => result.type === 'graphic')
        .map((result) => (result as unknown as { graphic: Graphic }).graphic)
        .at(0);

      if (!graphic) {
        return;
      }

      const objectId = graphic.getAttribute('OBJECTID') as number | null;
      if (objectId === null) {
        return; // A cluster, not an individual feature.
      }

      const project = await this.lookupByObjectId(objectId);
      if (project) {
        this.bridge.selectFromMap(project.id);
        this.urlState.writeSelected(project.id);
      }
    } catch (error) {
      this.reportLayerFailure(error);
    }
  }

  // The layer carries the project code, not the row id, so the lookup goes via the API.
  private readonly objectIdCache = new Map<number, Promise<ProjectPointSummary | null>>();

  private lookupByObjectId(objectId: number): Promise<ProjectPointSummary | null> {
    const cached = this.objectIdCache.get(objectId);
    if (cached) return cached;

    const request = (async () => {
      const feature = await this.projectLayer.queryFeatures({
        objectIds: [objectId],
        outFields: ['name'],
        returnGeometry: false, // Geometry is not used here; skip it to keep the payload small.
      });

      const code = feature.features[0]?.getAttribute('name') as string | undefined;
      if (!code) return null;

      const page = await new Promise<PagedResult<ProjectListItem>>((resolve, reject) => {
        this.api
          .page({
            ...emptyLookupQuery,
            search: code,
          })
          .subscribe({ next: resolve, error: reject });
      });

      const match = page.items.find((p) => p.projectCode === code) ?? null;
      return match
        ? {
            id: match.id,
            name: match.name,
            projectCode: match.projectCode,
            projectTypeName: match.projectTypeName,
            startDate: match.startDate,
            endDate: match.endDate,
            activityCount: match.activityCount,
          }
        : null;
    })().catch(() => null);

    this.objectIdCache.set(objectId, request);
    return request;
  }

  // ----- selection highlight -------------------------------------------------

  private async highlight(projectId: number | null): Promise<void> {
    this.highlightHandle?.remove();
    this.highlightHandle = null;
    this.urlState.writeSelected(projectId);

    if (projectId === null || !this.view) {
      return;
    }

    try {
      const layerView = await this.view.whenLayerView(this.projectLayer);

      // Find the feature whose `name` is this project's code.
      const code = await this.projectCodeFor(projectId);
      if (!code) return;

      const result = await this.projectLayer.queryFeatures({
        where: `name = '${code.replace(/'/g, "''")}'`,
        outFields: ['OBJECTID'],
        returnGeometry: false,
      });

      const objectId = result.features[0]?.getAttribute('OBJECTID') as number | undefined;
      if (objectId === undefined) return;

      this.highlightHandle = layerView.highlight(objectId);
    } catch (error) {
      this.reportLayerFailure(error);
    }
  }

  private async projectCodeFor(projectId: number): Promise<string | null> {
    for (const [, promise] of this.objectIdCache) {
      const value = await promise;
      if (value?.id === projectId) return value.projectCode;
    }

    return new Promise((resolve) => {
      this.api.get(projectId).subscribe({
        next: (project) => resolve(project.projectCode),
        error: () => resolve(null),
      });
    });
  }

  // ----- the draggable location pin ------------------------------------------

  private renderLocationGraphic(point: { latitude: number; longitude: number } | null): void {
    this.editLayer.removeAll();
    this.locationGraphic = null;

    if (!point) return;

    this.locationGraphic = new Graphic({
      geometry: new Point({ latitude: point.latitude, longitude: point.longitude }),
      symbol: {
        type: 'simple-marker',
        style: 'circle',
        color: '#f97316',
        size: 14,
        outline: { color: '#ffffff', width: 2 },
      },
    });

    this.editLayer.add(this.locationGraphic);
  }

  // Dragging the placed point.
  onPointerDown(event: PointerEvent): void {
    if (!this.view || !this.locationGraphic) return;

    void this.view.hitTest(event, { include: [this.editLayer] }).then((hit) => {
      if (hit.results.length > 0) {
        this.draggingLocation = true;
      }
    });
  }

  onPointerMove(event: PointerEvent): void {
    if (!this.draggingLocation || !this.view || !this.locationGraphic) return;

    event.preventDefault();
    const mapPoint = this.view.toMap({ x: event.offsetX, y: event.offsetY });
    if (!mapPoint) return;

    const { latitude, longitude } = this.toLatLng(mapPoint);
    this.locationGraphic.geometry = new Point({ latitude, longitude });
    this.bridge.draftLocation.set({ latitude, longitude });
  }

  onPointerUp(): void {
    this.draggingLocation = false;
  }

  // ----- sketch spatial filter -----------------------------------------------

  // A drawn rectangle or polygon filters the list.
  onSketchCreate(event: Event): void {
    // arcgis-sketch emits arcgisCreate / arcgisUpdate / arcgisDelete. The similarly
    // named arcgisSketch* events belong to arcgis-editor, and binding those to this
    // element registers listeners for events that are never dispatched.
    //
    // The two payloads differ: CreateEvent carries a single `graphic`, UpdateEvent an
    // array of `graphics`, so reshaping an existing polygon must be read from the array.
    const detail = (event as CustomEvent<SketchEventDetail>).detail;
    if (detail?.state !== 'complete') return;

    const polygon = (detail.graphic?.geometry ?? detail.graphics?.[0]?.geometry) as
      Polygon | null | undefined;
    if (!polygon) return;

    const wgs84 =
      polygon.spatialReference?.isWGS84 === true
        ? polygon
        : (webMercatorUtils.webMercatorToGeographic(polygon) as Polygon);

    this.bridge.sketchWkt.set(toWkt(wgs84));
  }

  onSketchDelete(): void {
    this.bridge.sketchWkt.set(null);
  }

  // ----- widgets and failures ------------------------------------------------

  // The brief requires the Search widget to query the project layer only.
  onSearchReady(event: Event): void {
    const search = event.target as HTMLElement & { sources: unknown };

    search.sources = [
      new LayerSearchSource({
        layer: this.projectLayer,
        name: 'Projects',
        placeholder: 'Search projects…',
        searchFields: [APP_CONFIG.arcgis.searchField],
        displayField: APP_CONFIG.arcgis.searchField,
        outFields: ['OBJECTID', 'name', 'SOURCEID'],
        exactMatch: false,
      }),
    ];
  }

  private reportLayerFailure(error: unknown): void {
    const message =
      error instanceof Error ? error.message : 'The project layer could not be queried.';

    this.notifications.error(`Map layer error: ${message}`, () => {
      this.objectIdCache.clear();
      void this.projectLayer.refresh();
    });
  }
}

// A minimal query used only to resolve one project code to its row.
const emptyLookupQuery = {
  page: 1,
  pageSize: 5,
  sort: 'projectCode' as const,
  dir: 'asc' as const,
  search: '',
  typeIds: [] as readonly number[],
  statuses: [] as readonly string[],
  dateFrom: null,
  dateTo: null,
  bbox: null,
  polygonWkt: null,
};

// Well-known text for a single-ring polygon, which is all the Sketch widget produces here.
function toWkt(polygon: Polygon): string {
  const ring = polygon.rings[0] ?? [];

  // WKT requires a closed ring. ArcGIS closes its rings, but an unclosed one makes
  // PostGIS throw "geometry requires more points" rather than return no rows, so the
  // closure is asserted here instead of assumed.
  const first = ring[0];
  const last = ring[ring.length - 1];
  const closed =
    first && last && (first[0] !== last[0] || first[1] !== last[1]) ? [...ring, first] : ring;

  const coordinates = closed.map(([x, y]) => `${x} ${y}`).join(', ');
  return `POLYGON((${coordinates}))`;
}

function escapeHtml(value: string): string {
  return value.replace(
    /[&<>"']/g,
    (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]!,
  );
}
